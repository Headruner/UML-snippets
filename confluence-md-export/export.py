#!/usr/bin/env python3
"""
Confluence -> Markdown space exporter.

Fetches page bodies in STORAGE format (fast; avoids the macro-render timeout that
hits the 'view'/export endpoints), converts each to Markdown via convert.py, and
writes a mirror under output/. Optionally downloads image attachments.

Auth: Atlassian Cloud API token (https://id.atlassian.com/manage-profile/security/api-tokens).
      Uses HTTP Basic with your account email + token. NEVER your password.

Usage:
    export ATLASSIAN_EMAIL="you@company.com"
    export ATLASSIAN_TOKEN="xxxxxxxx"
    python3 export.py --config export.yaml
    python3 export.py --config export.yaml --set core        # only the 31 core specs
    python3 export.py --config export.yaml --images          # also download attachments
    python3 export.py --config export.yaml --resume          # skip pages already written

Design notes (learned against this space):
 * Use /wiki/rest/api/content/{id}?expand=body.storage,version,ancestors  -> storage XHTML.
   The 'body.view' / export-word paths render macros server-side and can hang >300s on
   heavy pages. Storage format returns almost instantly.
 * Enumerate with /wiki/rest/api/content?spaceKey=KEY&type=page&limit=100 and follow
   the `_links.next` cursor. (CQL search also works but the search cursor must be URL
   decoded before reuse; the content endpoint's next link is simpler.)
"""
import os, sys, json, time, csv, argparse, base64, urllib.parse, urllib.request, urllib.error

try:
    import yaml
except ImportError:
    yaml = None

from convert import convert_page, slug, META, PAGES

HERE = os.path.dirname(os.path.abspath(__file__))


def load_config(path):
    text = open(path).read()
    if yaml:
        return yaml.safe_load(text)
    # minimal fallback parser for flat key: value YAML if PyYAML absent
    cfg = {}
    for line in text.splitlines():
        line = line.split('#', 1)[0].rstrip()
        if not line or ':' not in line:
            continue
        k, v = line.split(':', 1)
        cfg[k.strip()] = v.strip().strip('"').strip("'")
    return cfg


def auth_header():
    email = os.environ.get('ATLASSIAN_EMAIL')
    token = os.environ.get('ATLASSIAN_TOKEN')
    if not email or not token:
        sys.exit('ERROR: set ATLASSIAN_EMAIL and ATLASSIAN_TOKEN environment variables.')
    raw = f'{email}:{token}'.encode()
    return 'Basic ' + base64.b64encode(raw).decode()


def api_get(base_url, path, params=None, binary=False):
    url = base_url.rstrip('/') + path
    if params:
        url += '?' + urllib.parse.urlencode(params)
    req = urllib.request.Request(url, headers={
        'Authorization': auth_header(),
        'Accept': '*/*' if binary else 'application/json',
    })
    for attempt in range(4):
        try:
            with urllib.request.urlopen(req, timeout=1200) as r:
                data = r.read()
                return data if binary else json.loads(data)
        except urllib.error.HTTPError as e:
            if e.code in (429, 500, 502, 503, 504) and attempt < 3:
                wait = 2 ** attempt
                sys.stderr.write(f'  {e.code} on {path}; retry in {wait}s\n')
                time.sleep(wait)
                continue
            raise
    raise RuntimeError('unreachable')


def enumerate_space(base_url, space_key):
    """Yield {id,title} for every current page in the space, following cursors."""
    path = '/wiki/rest/api/content'
    params = {'spaceKey': space_key, 'type': 'page', 'status': 'current', 'limit': 100}
    while True:
        data = api_get(base_url, path, params)
        for r in data.get('results', []):
            yield {'id': r['id'], 'title': r['title']}
        nxt = (data.get('_links') or {}).get('next')
        if not nxt:
            break
        # next is a relative path already carrying its cursor
        path = nxt
        params = None


def fetch_page(base_url, page_id):
    data = api_get(base_url, f'/wiki/rest/api/content/{page_id}',
                   {'expand': 'body.storage,version,ancestors'})
    return {
        'id': data['id'],
        'title': data['title'],
        'version': {
            'number': data.get('version', {}).get('number', '?'),
            'createdAt': data.get('version', {}).get('when', '')[:10],
        },
        'webUrl': base_url.rstrip('/') + (data.get('_links', {}) or {}).get('webui', ''),
        'body': data.get('body', {}).get('storage', {}).get('value', ''),
        'ancestors': [a.get('title', '') for a in data.get('ancestors', [])],
    }


def list_attachments(base_url, page_id):
    """Return {filename: download_path} for a page's attachments."""
    out = {}
    start = 0
    while True:
        data = api_get(base_url, f'/wiki/rest/api/content/{page_id}/child/attachment',
                       {'limit': 50, 'start': start})
        for r in data.get('results', []):
            dl = (r.get('_links', {}) or {}).get('download')
            if dl:
                out[r['title']] = dl
        if data.get('size', 0) < data.get('limit', 50):
            break
        start += data.get('limit', 50)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--config', default=os.path.join(HERE, 'export.yaml'))
    ap.add_argument('--set', choices=['all', 'core'], default='all',
                    help="'all' = enumerate whole space; 'core' = only meta/core_set.json")
    ap.add_argument('--images', action='store_true', help='download image attachments')
    ap.add_argument('--resume', action='store_true', help='skip pages already written')
    ap.add_argument('--sleep', type=float, default=0.3, help='delay between pages (politeness)')
    args = ap.parse_args()

    cfg = load_config(args.config)
    base_url = cfg['base_url']
    space_key = cfg['space_key']
    out_root = os.path.join(HERE, cfg.get('output_dir', 'output'))

    global PAGES, META
    import convert
    convert.PAGES = os.path.join(out_root, 'pages')
    convert.META = os.path.join(out_root, 'meta')
    os.makedirs(convert.PAGES, exist_ok=True)
    os.makedirs(convert.META, exist_ok=True)
    assets = os.path.join(out_root, 'assets')
    os.makedirs(assets, exist_ok=True)

    # choose page set
    if args.set == 'core':
        pages = json.load(open(os.path.join(HERE, 'meta', 'core_set.json')))
    else:
        print(f'Enumerating space {space_key} ...')
        pages = list(enumerate_space(base_url, space_key))
        json.dump(pages, open(os.path.join(convert.META, 'all_pages.json'), 'w'), indent=1)
    print(f'{len(pages)} pages to process (set={args.set}).')

    manifest_path = os.path.join(convert.META, 'manifest.csv')
    man = open(manifest_path, 'w', newline='')
    mw = csv.writer(man)
    mw.writerow(['page_id', 'title', 'file', 'version', 'image_count', 'status'])

    img_csv = open(os.path.join(convert.META, 'images.csv'), 'w', newline='')
    iw = csv.writer(img_csv)
    iw.writerow(['page_id', 'page_file', 'image_filename', 'downloaded'])

    for i, p in enumerate(pages, 1):
        pid, title = p['id'], p['title']
        target = os.path.join(convert.PAGES, slug(title) + '.md')
        if args.resume and os.path.exists(target):
            print(f'[{i}/{len(pages)}] skip (exists): {title}')
            mw.writerow([pid, title, os.path.basename(target), '', '', 'skipped'])
            continue
        try:
            rec = fetch_page(base_url, pid)
            fn, imgs = convert_page(rec)
            status = 'ok'
        except Exception as e:
            print(f'[{i}/{len(pages)}] FAILED {title}: {e}')
            mw.writerow([pid, title, '', '', '', f'error: {e}'])
            continue

        downloaded = {}
        if args.images and imgs:
            att = {}
            try:
                att = list_attachments(base_url, pid)
            except Exception as e:
                sys.stderr.write(f'  attachment list failed for {pid}: {e}\n')
            for fname in set(imgs):
                if fname in att:
                    try:
                        blob = api_get(base_url, att[fname], binary=True)
                        with open(os.path.join(assets, fname), 'wb') as f:
                            f.write(blob)
                        downloaded[fname] = True
                    except Exception as e:
                        sys.stderr.write(f'  download failed {fname}: {e}\n')
                        downloaded[fname] = False

        for im in imgs:
            iw.writerow([pid, fn, im, downloaded.get(im, False)])
        mw.writerow([pid, title, fn, rec['version']['number'], len(imgs), status])
        print(f'[{i}/{len(pages)}] {status}: {title}  ({len(imgs)} imgs)')
        man.flush(); img_csv.flush()
        time.sleep(args.sleep)

    man.close(); img_csv.close()
    print(f'\nDone. Output in {out_root}\n  pages/  -> Markdown files\n  assets/ -> images (if --images)\n  meta/manifest.csv, meta/images.csv')


if __name__ == '__main__':
    main()
