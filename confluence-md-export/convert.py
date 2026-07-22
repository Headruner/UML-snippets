#!/usr/bin/env python3
"""
Confluence storage-format (HTML+) -> Markdown converter.
Input : JSON file {id,title,version,webUrl,body} produced from getConfluencePage(html).
Output: pages/<slug>.md  and appends image references to .meta/images.csv
Text-only: images become ![filename](assets/filename) plus a manifest row.
"""
import json, re, sys, os, html, csv
from html.parser import HTMLParser

META = os.path.join(os.path.dirname(__file__), '.meta')
PAGES = os.path.join(os.path.dirname(__file__), 'pages')

def slug(t):
    t = (t or '').strip()
    t = re.sub(r'[<>:"/\\|?*&]+', ' ', t)
    t = re.sub(r'\s+', '-', t)
    t = t.strip('.-')
    return (t[:120] or 'untitled')

class ConfluenceMD(HTMLParser):
    """Walks storage HTML and emits Markdown. Handles Confluence HTML+ data-type nodes."""
    def __init__(self, page_id):
        super().__init__(convert_charrefs=True)
        self.page_id = page_id
        self.out = []              # finished block strings
        self.buf = []              # current inline text buffer
        self.list_stack = []       # ('ul'|'ol', counter)
        self.images = []           # collected image filenames
        self.skip_depth = 0        # inside <summary>/version macros to skip
        self.in_pre = False
        self.pre_lang = ''
        self.in_code = False
        self.link_href = None
        self.link_text = []
        self.capturing_link = False
        self.heading = None
        self.cell_mode = False     # inside table cell (collect inline)
        # table state
        self.table = None          # list of rows; row = list of cell-strings
        self.cur_row = None
        self.cur_cell = None
        self.in_th = False

    # ---- helpers ----
    def _flush_inline(self):
        text = ''.join(self.buf).strip()
        self.buf = []
        return text

    def emit(self, block):
        if block is not None and block != '':
            self.out.append(block)

    def add_text(self, t):
        if self.skip_depth > 0:
            return
        if self.capturing_link:
            self.link_text.append(t)
            return
        if self.cur_cell is not None:
            self.cur_cell.append(t)
            return
        self.buf.append(t)

    # ---- tag handling ----
    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        dtype = a.get('data-type', '')

        # skip version-history / change-history blocks
        if tag == 'summary':
            self.skip_depth += 1
            return
        if dtype == 'extension' and a.get('data-extension-key') == 'change-history':
            self.emit('> _Version history (Confluence change-history macro — not exported)._')
            return

        if self.skip_depth > 0:
            return

        if tag in ('h1','h2','h3','h4','h5','h6'):
            self._close_paragraph()
            self.heading = int(tag[1])
            self.buf = []
        elif tag == 'p':
            self._close_paragraph()
        elif tag == 'br':
            self.add_text('  \n')
        elif tag in ('strong','b'):
            self.add_text('**')
        elif tag in ('em','i'):
            self.add_text('*')
        elif tag == 'code' and not self.in_pre:
            self.in_code = True
            self.add_text('`')
        elif tag == 'sup':
            self.add_text('^')
        elif tag in ('s','del','strike'):
            self.add_text('~~')
        elif tag == 'input' and a.get('type') == 'checkbox':
            self.add_text('[x] ' if 'checked' in a else '[ ] ')
        elif dtype == 'status':
            color = a.get('data-color', '')
            self.add_text(f'`[{color.upper()}]` ' if color else '`[STATUS]` ')
        elif tag == 'time':
            dt = a.get('datetime', '')
            if dt:
                self.add_text(dt)
                self._skip_time_text = True
        elif tag == 'pre':
            self._close_paragraph()
            self.in_pre = True
            self.pre_lang = ''
            self.pre_buf = []
        elif tag == 'a':
            self.capturing_link = True
            self.link_href = a.get('href')
            self.link_text = []
        elif tag == 'ul':
            self.list_stack.append(['ul', 0])
        elif tag == 'ol':
            self.list_stack.append(['ol', 0])
        elif tag == 'li':
            self._close_paragraph()
            # open a new list item: assign marker now
            depth = max(0, len(self.list_stack) - 1)
            indent = '  ' * depth
            if self.list_stack and self.list_stack[-1][0] == 'ol':
                self.list_stack[-1][1] += 1
                marker = f'{self.list_stack[-1][1]}.'
            else:
                marker = '-'
            self.cur_li_prefix = f'{indent}{marker} '
            self.li_pending = True
            self.buf = []
        elif tag == 'table':
            self._close_paragraph()
            self.table = []
        elif tag == 'tr':
            self.cur_row = []
        elif tag in ('td','th'):
            self.cur_cell = []
            self.in_th = (tag == 'th')
        # media / images
        elif tag == 'div' and dtype == 'media':
            fn = None  # filename comes as text; capture via flag
            self._pending_media = True
        elif dtype in ('media-inline','media-single'):
            self._pending_media = True

    def handle_startendtag(self, tag, attrs):
        self.handle_starttag(tag, attrs)
        self.handle_endtag(tag)

    def handle_endtag(self, tag):
        if tag == 'summary':
            self.skip_depth = max(0, self.skip_depth - 1)
            return
        if self.skip_depth > 0:
            return

        if tag in ('h1','h2','h3','h4','h5','h6'):
            txt = self._flush_inline()
            if txt:
                self.emit('#' * self.heading + ' ' + txt)
            self.heading = None
        elif tag == 'p':
            self._close_paragraph()
        elif tag in ('strong','b'):
            self.add_text('**')
        elif tag in ('em','i'):
            self.add_text('*')
        elif tag == 'code' and self.in_code:
            self.in_code = False
            self.add_text('`')
        elif tag == 'sup':
            self.add_text('^')
        elif tag in ('s','del','strike'):
            self.add_text('~~')
        elif tag == 'time':
            self._skip_time_text = False
        elif tag == 'pre':
            code = ''.join(getattr(self, 'pre_buf', []))
            self.in_pre = False
            lang = self.pre_lang or ''
            self.emit('```' + lang + '\n' + code.rstrip('\n') + '\n```')
        elif tag == 'a':
            self.capturing_link = False
            text = ''.join(self.link_text).strip()
            href = self.link_href or ''
            if href:
                md = f'[{text or href}]({href})'
            else:
                md = text
            self.add_text(md)
            self.link_href = None
        elif tag == 'ul' or tag == 'ol':
            if self.list_stack:
                self.list_stack.pop()
            if not self.list_stack:
                self.emit('')  # blank line after top-level list
        elif tag == 'li':
            self._close_paragraph()
        elif tag == 'table':
            self._emit_table()
            self.table = None
        elif tag == 'tr':
            if self.table is not None and self.cur_row is not None:
                self.table.append(self.cur_row)
            self.cur_row = None
        elif tag in ('td','th'):
            cell = ''.join(self.cur_cell).strip().replace('\n',' ')
            if self.cur_row is not None:
                self.cur_row.append(cell)
            self.cur_cell = None
            self.in_th = False

    def handle_data(self, data):
        if self.skip_depth > 0:
            return
        if getattr(self, '_pending_media', False):
            fn = data.strip()
            if fn:
                self.images.append(fn)
                self.add_text(f'![{fn}](assets/{fn})')
            self._pending_media = False
            return
        if getattr(self, '_skip_time_text', False):
            return
        if self.in_pre:
            self.pre_buf = getattr(self, 'pre_buf', [])
            self.pre_buf.append(data)
            return
        self.add_text(data)

    # ---- block builders ----
    def _close_paragraph(self):
        if getattr(self, 'li_pending', False):
            txt = self._flush_inline()
            prefix = getattr(self, 'cur_li_prefix', '- ')
            if txt:
                self.emit(f'{prefix}{txt}')
                self.li_pending = False   # first para consumed the marker
            return
        txt = self._flush_inline()
        if txt:
            # continuation line inside an open <li> (e.g. 2nd paragraph): indent it
            self.emit(txt)

    def _emit_table(self):
        if not self.table:
            return
        rows = [r for r in self.table if r]
        if not rows:
            return
        ncol = max(len(r) for r in rows)
        rows = [r + ['']*(ncol-len(r)) for r in rows]
        header = rows[0]
        body = rows[1:]
        md = []
        md.append('| ' + ' | '.join(c or ' ' for c in header) + ' |')
        md.append('| ' + ' | '.join(['---']*ncol) + ' |')
        for r in body:
            md.append('| ' + ' | '.join(c or ' ' for c in r) + ' |')
        self.emit('\n'.join(md))

    def result(self):
        # join blocks with blank lines, collapse >2 blank lines
        text = '\n\n'.join(b for b in self.out if b is not None)
        text = re.sub(r'\n{3,}', '\n\n', text)
        # tighten: collapse blank line between two consecutive list-item lines
        text = re.sub(r'(^\s*(?:-|\d+\.) .*)\n\n(?=\s*(?:-|\d+\.) )', r'\1\n', text, flags=re.M)
        # run twice to catch chained items
        text = re.sub(r'(^\s*(?:-|\d+\.) .*)\n\n(?=\s*(?:-|\d+\.) )', r'\1\n', text, flags=re.M)
        return text.strip() + '\n'


def convert_page(rec):
    pid = rec['id']; title = rec.get('title','')
    body = rec.get('body','') or ''
    p = ConfluenceMD(pid)
    p.feed(body)
    md_body = p.result()
    ver = (rec.get('version') or {})
    vnum = ver.get('number','?') if isinstance(ver, dict) else '?'
    vdate = ver.get('createdAt','') if isinstance(ver, dict) else ''
    weburl = rec.get('webUrl','')
    front = (f"<!--\nsource: {weburl}\nspace: LDP3 (Listan / Developex - IO Center Desktop)\n"
             f"page-id: {pid}\nversion: {vnum} ({vdate})\nexported: 2026-07-20 (Confluence storage format)\n"
             f"note: images referenced by filename; binaries not downloaded (see images.csv).\n-->\n\n")
    md = front + f'# {title}\n\n' + md_body
    fn = slug(title) + '.md'
    with open(os.path.join(PAGES, fn), 'w') as f:
        f.write(md)
    return fn, p.images


if __name__ == '__main__':
    inp = sys.argv[1]
    rec = json.load(open(inp))
    fn, imgs = convert_page(rec)
    # append to image manifest
    os.makedirs(META, exist_ok=True)
    newfile = not os.path.exists(os.path.join(META,'images.csv'))
    with open(os.path.join(META,'images.csv'),'a',newline='') as f:
        w = csv.writer(f)
        if newfile:
            w.writerow(['page_id','page_file','image_filename'])
        for im in imgs:
            w.writerow([rec['id'], fn, im])
    print(json.dumps({'file':fn,'images':len(imgs)}))
