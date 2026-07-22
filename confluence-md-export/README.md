# Confluence → Markdown Space Exporter

Exports a Confluence Cloud space to a local mirror of Markdown files. Built and
tested against **LDP3** ("Listan / Developex – IO Center Desktop", 225 pages).

Two equivalent implementations are included — use whichever fits your environment:

- **Python** (`export.py` + `convert.py`) — zero-build, needs only Python 3.9+.
- **C#** (`csharp/`) — .NET 8 console app, matches a C#-primary stack.

Both produce identical output structure and use the same node-conversion policy.

---

## Why this exists (and the one thing that matters)

The obvious approach — asking Confluence for the *rendered* page body or a Word/PDF
export — re-expands every macro server-side. On heavy pages that request **hangs past
the 300 s timeout**. This exporter instead requests the **storage format**
(`expand=body.storage`), which is the raw XHTML Confluence stores and returns almost
instantly. Every large page in the target space came back in well under a second this way.

The converter then translates that storage XHTML — including Confluence's `data-type`
HTML+ nodes (panels, status badges, task lists, mentions, media, dates) — into clean
Markdown.

---

## Prerequisites

1. An Atlassian Cloud **API token**: https://id.atlassian.com/manage-profile/security/api-tokens
   Use your account **email + token** (HTTP Basic). Never your password.
2. Credentials via environment variables (never committed to the config file):

   ```bash
   export ATLASSIAN_EMAIL="you@company.com"
   export ATLASSIAN_TOKEN="xxxxxxxxxxxx"
   ```
   (Windows PowerShell: `$env:ATLASSIAN_EMAIL="..."` / `$env:ATLASSIAN_TOKEN="..."`)

3. Edit `export.yaml`:

   ```yaml
   base_url: https://bequiet.atlassian.net   # site root, no trailing /wiki
   space_key: LDP3
   output_dir: output
   ```

---

## Running — Python

```bash
python3 export.py --config export.yaml              # whole space (enumerates all pages)
python3 export.py --config export.yaml --set core   # only the 31 curated specs (meta/core_set.json)
python3 export.py --config export.yaml --images     # also download image attachments into assets/
python3 export.py --config export.yaml --resume     # skip pages already written (safe re-run)
```

`--images` and `--resume` combine with either set. PyYAML is optional; without it a
minimal flat-key parser handles the config.

## Running — C#

```bash
cd csharp
dotnet restore
dotnet run -- --config ../export.yaml               # whole space
dotnet run -- --config ../export.yaml --set core --images --resume
```

---

## Output layout

```
output/
├── pages/          # one <slug>.md per Confluence page
├── assets/         # image binaries (only when --images is used)
└── meta/
    ├── all_pages.json   # full inventory captured during enumeration
    ├── manifest.csv     # page_id, title, file, version, image_count, status
    └── images.csv       # page_id, page_file, image_filename, downloaded
```

Each `.md` file opens with an HTML comment carrying provenance (source URL, page id,
version, ancestor path, export date) so the mirror is auditable.

---

## What the converter does with each element

| Confluence element                         | Markdown result                          |
|--------------------------------------------|------------------------------------------|
| `h1`–`h6`, `p`, `ul`/`ol` (nested), tables | native Markdown equivalents              |
| bold / italic / strikethrough / `sup`      | `**` / `*` / `~~` / `^`                   |
| inline `code`, `pre` / code macro          | `` `code` `` / fenced ```` ``` ```` block |
| links, inline cards                        | `[text](href)`                           |
| media / media-inline (images)              | `![file](assets/file)` + `images.csv` row |
| status lozenge                             | `` `[COLOR]` ``                          |
| task list checkbox                         | `[x]` / `[ ]`                            |
| mention                                    | `@Name` text                             |
| `time[datetime]`                           | ISO date (`2026-07-20`)                  |
| toc, anchor, change/version-history macros | dropped (navigation/meta only)           |

Images are **referenced by filename** in the text; the binaries are downloaded only
when you pass `--images`. `images.csv` is the manifest of every referenced file and
whether it was fetched — so a text-only run still leaves you a complete download list.

---

## Scope control

- `meta/all_pages.json` — the full 225-page inventory (id + title), captured live.
- `meta/core_set.json` — a curated 31-page subset: every device **SRD / FRD** and the
  top-level device/project specs (P1 PSU, W1/W2 coolers, F1 fans, A1 air cooler,
  K3/M3 keyboard & mice, SC/BC controllers). Use `--set core` to export just these.

To define your own subset, drop an `[{"id": "...", "title": "..."}]` JSON list in
place of `core_set.json` (or point the code at another file).

---

## Operational notes

- **Pagination**: enumeration follows `_links.next` from the content endpoint, which
  carries its own cursor — no manual offset math. (The CQL *search* endpoint also
  paginates, but its cursor must be URL-decoded before reuse; the content endpoint is
  simpler and is what's used here.)
- **Retries**: 429/5xx responses back off exponentially (up to 3 retries).
- **Politeness**: a 300 ms delay between pages keeps well under rate limits; tune with
  `--sleep` (Python) or edit the `Task.Delay` in `Program.cs`.
- **Resumability**: `--resume` skips any page whose `.md` already exists, so an
  interrupted run continues cleanly. The manifest records `ok` / `skipped` / `error`
  per page; failures don't stop the batch.
- **Internal links**: cross-page links currently keep their original Confluence URL.
  If you want them rewritten to local `.md` targets, resolve `page_id`→`slug` from
  `manifest.csv` in a post-pass — a natural next enhancement.

---

## Architecture

See `docs/pipeline.puml` (PlantUML). Render with any PlantUML tool, e.g.:

```bash
plantuml docs/pipeline.puml     # -> docs/pipeline.png
```
