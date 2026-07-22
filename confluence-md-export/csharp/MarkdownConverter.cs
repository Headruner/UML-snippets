using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace ConfluenceMdExport;

/// <summary>
/// Converts Confluence storage-format XHTML (HTML+) into GitHub-flavoured Markdown.
/// Node policy mirrors the reference Python converter:
///   headings, paragraphs, bold/italic/strike/sup, inline code, fenced pre,
///   nested ul/ol, tables, links; Confluence HTML+ data-type nodes:
///     media / media-inline -> image reference (+ collected filename)
///     status               -> `[COLOR]`
///     task-item/checkbox    -> [x] / [ ]
///     mention               -> @Name text
///     time[datetime]        -> ISO date
///     change/version history, toc, anchor -> dropped
/// </summary>
public sealed class MarkdownConverter
{
    private readonly StringBuilder _out = new();
    private readonly List<string> _images = new();

    public IReadOnlyList<string> Images => _images;

    public static string Slug(string title)
    {
        var t = (title ?? "").Trim();
        t = Regex.Replace(t, "[<>:\"/\\\\|?*&]+", " ");
        t = Regex.Replace(t, "\\s+", "-").Trim('.', '-');
        if (t.Length > 120) t = t[..120];
        return string.IsNullOrEmpty(t) ? "untitled" : t;
    }

    public string Convert(ConfluencePage page)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(page.StorageBody ?? "");
        var body = new StringBuilder();
        foreach (var node in doc.DocumentNode.ChildNodes)
            body.Append(RenderBlock(node));

        var md = Regex.Replace(body.ToString(), "\n{3,}", "\n\n");
        // tighten consecutive list items
        for (var i = 0; i < 2; i++)
            md = Regex.Replace(md, @"(?m)(^\s*(?:-|\d+\.) .*)\n\n(?=\s*(?:-|\d+\.) )", "$1\n");
        md = md.Trim() + "\n";

        var front =
            $"<!--\nsource: {page.WebUrl}\nspace-page-id: {page.Id}\n" +
            $"version: {page.Version} ({page.VersionDate})\n" +
            $"ancestors: {string.Join(" / ", page.Ancestors)}\n" +
            $"exported: {DateTime.UtcNow:yyyy-MM-dd} (Confluence storage format)\n" +
            "note: images referenced by filename; see images.csv.\n-->\n\n";

        return front + $"# {page.Title}\n\n" + md;
    }

    // ---- block-level ----
    private string RenderBlock(HtmlNode n)
    {
        switch (n.Name.ToLowerInvariant())
        {
            case "#text":
                var txt = Clean(n.InnerText);
                return string.IsNullOrWhiteSpace(txt) ? "" : txt + "\n\n";
            case "h1": case "h2": case "h3": case "h4": case "h5": case "h6":
                var level = int.Parse(n.Name[1..]);
                return new string('#', level) + " " + Inline(n).Trim() + "\n\n";
            case "p":
                var p = Inline(n).Trim();
                return p.Length == 0 ? "" : p + "\n\n";
            case "ul": case "ol":
                return RenderList(n, 0) + "\n";
            case "pre":
                return "```\n" + n.InnerText.TrimEnd('\n') + "\n```\n\n";
            case "table":
                return RenderTable(n) + "\n\n";
            case "figure":
            case "div":
                if (IsSkippable(n)) return "";
                if (IsMedia(n)) { var img = Media(n); return img.Length == 0 ? "" : img + "\n\n"; }
                var inner = new StringBuilder();
                foreach (var c in n.ChildNodes) inner.Append(RenderBlock(c));
                return inner.ToString();
            case "details":   // expand macro / version history wrapper
                if (IsSkippable(n)) return "";
                var d = new StringBuilder();
                foreach (var c in n.ChildNodes)
                    if (!c.Name.Equals("summary", StringComparison.OrdinalIgnoreCase))
                        d.Append(RenderBlock(c));
                return d.ToString();
            default:
                var sb = new StringBuilder();
                foreach (var c in n.ChildNodes) sb.Append(RenderBlock(c));
                return sb.ToString();
        }
    }

    private string RenderList(HtmlNode list, int depth)
    {
        var ordered = list.Name.Equals("ol", StringComparison.OrdinalIgnoreCase);
        var indent = new string(' ', depth * 2);
        var sb = new StringBuilder();
        var idx = 0;
        foreach (var li in list.ChildNodes)
        {
            if (!li.Name.Equals("li", StringComparison.OrdinalIgnoreCase)) continue;
            idx++;
            var marker = ordered ? $"{idx}." : "-";
            // first-line inline content (excluding nested lists)
            var inlineText = InlineExcludingLists(li).Trim();
            sb.Append($"{indent}{marker} {inlineText}\n");
            foreach (var child in li.ChildNodes)
                if (child.Name is "ul" or "ol")
                    sb.Append(RenderList(child, depth + 1));
        }
        return sb.ToString();
    }

    private string RenderTable(HtmlNode table)
    {
        var rows = new List<List<string>>();
        foreach (var tr in table.Descendants("tr"))
        {
            var cells = new List<string>();
            foreach (var td in tr.ChildNodes)
                if (td.Name is "td" or "th")
                    cells.Add(Inline(td).Trim().Replace("\n", " "));
            if (cells.Count > 0) rows.Add(cells);
        }
        if (rows.Count == 0) return "";
        var ncol = rows.Max(r => r.Count);
        foreach (var r in rows) while (r.Count < ncol) r.Add("");
        var sb = new StringBuilder();
        sb.Append("| " + string.Join(" | ", rows[0].Select(c => c.Length == 0 ? " " : c)) + " |\n");
        sb.Append("| " + string.Join(" | ", Enumerable.Repeat("---", ncol)) + " |\n");
        foreach (var r in rows.Skip(1))
            sb.Append("| " + string.Join(" | ", r.Select(c => c.Length == 0 ? " " : c)) + " |\n");
        return sb.ToString();
    }

    // ---- inline-level ----
    private string Inline(HtmlNode n) => InlineImpl(n, includeLists: true);
    private string InlineExcludingLists(HtmlNode n) => InlineImpl(n, includeLists: false);

    private string InlineImpl(HtmlNode n, bool includeLists)
    {
        var sb = new StringBuilder();
        foreach (var c in n.ChildNodes)
        {
            switch (c.Name.ToLowerInvariant())
            {
                case "#text": sb.Append(Clean(c.InnerText)); break;
                case "strong": case "b": sb.Append("**" + InlineImpl(c, includeLists).Trim() + "**"); break;
                case "em": case "i": sb.Append("*" + InlineImpl(c, includeLists).Trim() + "*"); break;
                case "s": case "del": case "strike": sb.Append("~~" + InlineImpl(c, includeLists).Trim() + "~~"); break;
                case "sup": sb.Append("^" + InlineImpl(c, includeLists)); break;
                case "code": sb.Append("`" + c.InnerText + "`"); break;
                case "br": sb.Append("  \n"); break;
                case "a":
                    var href = c.GetAttributeValue("href", "");
                    var text = InlineImpl(c, includeLists).Trim();
                    sb.Append(href.Length > 0 ? $"[{(text.Length > 0 ? text : href)}]({href})" : text);
                    break;
                case "time":
                    var dt = c.GetAttributeValue("datetime", "");
                    sb.Append(dt.Length > 0 ? dt : Clean(c.InnerText));
                    break;
                case "input":
                    if (c.GetAttributeValue("type", "") == "checkbox")
                        sb.Append(c.Attributes.Contains("checked") ? "[x] " : "[ ] ");
                    break;
                case "span":
                    var dtype = c.GetAttributeValue("data-type", "");
                    if (dtype == "status")
                    {
                        var color = c.GetAttributeValue("data-color", "").ToUpperInvariant();
                        sb.Append(color.Length > 0 ? $"`[{color}]` " : "`[STATUS]` ");
                        sb.Append(InlineImpl(c, includeLists));   // label
                    }
                    else if (dtype is "media-inline")
                    {
                        var fn = Clean(c.InnerText).Trim();
                        if (fn.Length > 0) { _images.Add(fn); sb.Append($"![{fn}](assets/{fn})"); }
                    }
                    else sb.Append(InlineImpl(c, includeLists));  // mention & plain spans keep text
                    break;
                case "ul": case "ol":
                    if (includeLists) sb.Append("\n" + RenderList(c, 1));
                    break;
                case "figure": case "div":
                    if (IsMedia(c)) sb.Append(Media(c));
                    else sb.Append(InlineImpl(c, includeLists));
                    break;
                case "p":
                    var inner = InlineImpl(c, includeLists).Trim();
                    if (inner.Length > 0) sb.Append(inner + " ");
                    break;
                default:
                    sb.Append(InlineImpl(c, includeLists));
                    break;
            }
        }
        return sb.ToString();
    }

    private string Media(HtmlNode n)
    {
        // media filename is the element's text; media-inline handled in Inline
        var media = n.Descendants().FirstOrDefault(d =>
            d.GetAttributeValue("data-type", "") == "media");
        var fn = media != null ? Clean(media.InnerText).Trim() : "";
        if (fn.Length == 0) return "";
        _images.Add(fn);
        return $"![{fn}](assets/{fn})";
    }

    private static bool IsMedia(HtmlNode n) =>
        n.GetAttributeValue("data-type", "") is "media" or "media-single"
        || n.Descendants().Any(d => d.GetAttributeValue("data-type", "") == "media");

    private static bool IsSkippable(HtmlNode n)
    {
        var key = n.GetAttributeValue("data-extension-key", "");
        if (key is "toc" or "anchor" or "change-history") return true;
        // version-history <details> whose summary says so
        var summary = n.SelectSingleNode(".//summary");
        if (summary != null && summary.InnerText.Contains("Version history", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static string Clean(string s) =>
        HtmlEntity.DeEntitize(s ?? "").Replace("\r", "");
}
