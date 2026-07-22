using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ConfluenceMdExport;

/// <summary>Minimal Confluence Cloud REST client.</summary>
/// <remarks>
/// Fetches page bodies in STORAGE format (expand=body.storage). The rendered/view and
/// export-word endpoints re-expand macros server-side and can hang &gt;300s on heavy pages;
/// storage format returns almost immediately. Enumeration follows the _links.next cursor.
/// Auth is HTTP Basic with account email + API token (never a password).
/// </remarks>
public sealed record PageRef(string Id, string Title);

public sealed record ConfluencePage(
    string Id,
    string Title,
    int Version,
    string VersionDate,
    string WebUrl,
    string StorageBody,
    IReadOnlyList<string> Ancestors);

public sealed record Attachment(string FileName, string DownloadPath);

public sealed class ConfluenceClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public ConfluenceClient(string baseUrl, string email, string token)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{token}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", raw);
    }

    private async Task<JsonElement> GetJsonAsync(string pathOrUrl)
    {
        var url = pathOrUrl.StartsWith("http") ? pathOrUrl : _baseUrl + pathOrUrl;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var resp = await _http.GetAsync(url);
                if ((int)resp.StatusCode is 429 or 500 or 502 or 503 or 504 && attempt < 3)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                    continue;
                }
                resp.EnsureSuccessStatusCode();
                await using var s = await resp.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(s);
                return doc.RootElement.Clone();
            }
            catch (HttpRequestException) when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }
    }

    /// <summary>Enumerate every current page in a space, following the next cursor.</summary>
    public async IAsyncEnumerable<PageRef> EnumerateSpaceAsync(string spaceKey)
    {
        var path = $"/wiki/rest/api/content?spaceKey={Uri.EscapeDataString(spaceKey)}"
                 + "&type=page&status=current&limit=100";
        while (true)
        {
            var data = await GetJsonAsync(path);
            foreach (var r in data.GetProperty("results").EnumerateArray())
                yield return new PageRef(
                    r.GetProperty("id").GetString()!,
                    r.GetProperty("title").GetString()!);

            if (data.TryGetProperty("_links", out var links) &&
                links.TryGetProperty("next", out var next))
                path = next.GetString()!;   // relative path carries its own cursor
            else
                break;
        }
    }

    public async Task<ConfluencePage> FetchPageAsync(string pageId)
    {
        var data = await GetJsonAsync(
            $"/wiki/rest/api/content/{pageId}?expand=body.storage,version,ancestors");

        var version = data.TryGetProperty("version", out var v)
            ? v.GetProperty("number").GetInt32() : 0;
        var vdate = data.TryGetProperty("version", out var v2) &&
                    v2.TryGetProperty("when", out var w)
            ? (w.GetString() ?? "")[..Math.Min(10, (w.GetString() ?? "").Length)] : "";
        var body = data.GetProperty("body").GetProperty("storage").GetProperty("value").GetString() ?? "";
        var webui = data.TryGetProperty("_links", out var l) && l.TryGetProperty("webui", out var wu)
            ? wu.GetString() ?? "" : "";
        var ancestors = new List<string>();
        if (data.TryGetProperty("ancestors", out var anc))
            foreach (var a in anc.EnumerateArray())
                if (a.TryGetProperty("title", out var t)) ancestors.Add(t.GetString() ?? "");

        return new ConfluencePage(
            data.GetProperty("id").GetString()!,
            data.GetProperty("title").GetString()!,
            version, vdate, _baseUrl + webui, body, ancestors);
    }

    public async Task<IReadOnlyList<Attachment>> ListAttachmentsAsync(string pageId)
    {
        var result = new List<Attachment>();
        var start = 0;
        while (true)
        {
            var data = await GetJsonAsync(
                $"/wiki/rest/api/content/{pageId}/child/attachment?limit=50&start={start}");
            foreach (var r in data.GetProperty("results").EnumerateArray())
            {
                var name = r.GetProperty("title").GetString()!;
                if (r.TryGetProperty("_links", out var l) && l.TryGetProperty("download", out var d))
                    result.Add(new Attachment(name, d.GetString()!));
            }
            var size = data.TryGetProperty("size", out var sz) ? sz.GetInt32() : 0;
            var limit = data.TryGetProperty("limit", out var lm) ? lm.GetInt32() : 50;
            if (size < limit) break;
            start += limit;
        }
        return result;
    }

    public async Task<byte[]> DownloadAsync(string downloadPath)
        => await _http.GetByteArrayAsync(
            downloadPath.StartsWith("http") ? downloadPath : _baseUrl + downloadPath);

    public void Dispose() => _http.Dispose();
}
