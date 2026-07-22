using System.Text;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ConfluenceMdExport;

/// <summary>
/// Console entry point. Fetches a Confluence space (or a curated id list) and writes
/// a Markdown mirror with a manifest. Storage-format fetch avoids macro-render timeouts.
///
///   set ATLASSIAN_EMAIL=you@company.com
///   set ATLASSIAN_TOKEN=your-api-token
///   dotnet run -- --config ../export.yaml            (whole space)
///   dotnet run -- --config ../export.yaml --set core (only meta/core_set.json ids)
///   dotnet run -- --config ../export.yaml --images   (also download attachments)
///   dotnet run -- --config ../export.yaml --resume   (skip files already written)
/// </summary>
public sealed class ExportConfig
{
    public string BaseUrl { get; set; } = "";
    public string SpaceKey { get; set; } = "";
    public string OutputDir { get; set; } = "output";
}

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var opts = ParseArgs(args);
        var cfgPath = opts.GetValueOrDefault("config", "export.yaml");
        var cfg = LoadConfig(cfgPath);

        var email = Environment.GetEnvironmentVariable("ATLASSIAN_EMAIL");
        var token = Environment.GetEnvironmentVariable("ATLASSIAN_TOKEN");
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        {
            Console.Error.WriteLine("ERROR: set ATLASSIAN_EMAIL and ATLASSIAN_TOKEN.");
            return 1;
        }

        var baseDir = Path.GetDirectoryName(Path.GetFullPath(cfgPath))!;
        var outRoot = Path.Combine(baseDir, cfg.OutputDir);
        var pagesDir = Path.Combine(outRoot, "pages");
        var metaDir = Path.Combine(outRoot, "meta");
        var assetsDir = Path.Combine(outRoot, "assets");
        Directory.CreateDirectory(pagesDir);
        Directory.CreateDirectory(metaDir);
        Directory.CreateDirectory(assetsDir);

        using var client = new ConfluenceClient(cfg.BaseUrl, email, token);

        // choose the page set
        List<PageRef> pages;
        if (opts.GetValueOrDefault("set") == "core")
        {
            var json = await File.ReadAllTextAsync(Path.Combine(baseDir, "meta", "core_set.json"));
            pages = JsonSerializer.Deserialize<List<PageRef>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }
        else
        {
            Console.WriteLine($"Enumerating space {cfg.SpaceKey} ...");
            pages = new List<PageRef>();
            await foreach (var pr in client.EnumerateSpaceAsync(cfg.SpaceKey)) pages.Add(pr);
            await File.WriteAllTextAsync(Path.Combine(metaDir, "all_pages.json"),
                JsonSerializer.Serialize(pages, new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine($"{pages.Count} pages to process.");

        var doImages = opts.ContainsKey("images");
        var resume = opts.ContainsKey("resume");

        await using var man = new StreamWriter(Path.Combine(metaDir, "manifest.csv"));
        await using var imgCsv = new StreamWriter(Path.Combine(metaDir, "images.csv"));
        await man.WriteLineAsync("page_id,title,file,version,image_count,status");
        await imgCsv.WriteLineAsync("page_id,page_file,image_filename,downloaded");

        var i = 0;
        foreach (var pr in pages)
        {
            i++;
            var file = MarkdownConverter.Slug(pr.Title) + ".md";
            var target = Path.Combine(pagesDir, file);
            if (resume && File.Exists(target))
            {
                Console.WriteLine($"[{i}/{pages.Count}] skip (exists): {pr.Title}");
                await man.WriteLineAsync(Csv(pr.Id, pr.Title, file, "", "", "skipped"));
                continue;
            }

            try
            {
                var page = await client.FetchPageAsync(pr.Id);
                var conv = new MarkdownConverter();
                var md = conv.Convert(page);
                await File.WriteAllTextAsync(target, md, new UTF8Encoding(false));

                var downloaded = new Dictionary<string, bool>();
                if (doImages && conv.Images.Count > 0)
                {
                    var atts = await client.ListAttachmentsAsync(pr.Id);
                    var byName = atts.ToDictionary(a => a.FileName, a => a.DownloadPath);
                    foreach (var fn in conv.Images.Distinct())
                    {
                        if (byName.TryGetValue(fn, out var dl))
                        {
                            try
                            {
                                var bytes = await client.DownloadAsync(dl);
                                await File.WriteAllBytesAsync(Path.Combine(assetsDir, fn), bytes);
                                downloaded[fn] = true;
                            }
                            catch (Exception e)
                            {
                                Console.Error.WriteLine($"  download failed {fn}: {e.Message}");
                                downloaded[fn] = false;
                            }
                        }
                    }
                }

                foreach (var im in conv.Images)
                    await imgCsv.WriteLineAsync(Csv(pr.Id, file, im,
                        downloaded.GetValueOrDefault(im) ? "true" : "false"));
                await man.WriteLineAsync(Csv(pr.Id, pr.Title, file,
                    page.Version.ToString(), conv.Images.Count.ToString(), "ok"));
                Console.WriteLine($"[{i}/{pages.Count}] ok: {pr.Title} ({conv.Images.Count} imgs)");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[{i}/{pages.Count}] FAILED {pr.Title}: {e.Message}");
                await man.WriteLineAsync(Csv(pr.Id, pr.Title, "", "", "", "error: " + e.Message));
            }
            await man.FlushAsync();
            await imgCsv.FlushAsync();
            await Task.Delay(300);   // politeness
        }

        Console.WriteLine($"\nDone. Output in {outRoot}");
        return 0;
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var d = new Dictionary<string, string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            var key = args[i][2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) { d[key] = args[++i]; }
            else d[key] = "true";
        }
        return d;
    }

    private static ExportConfig LoadConfig(string path)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<ExportConfig>(File.ReadAllText(path));
    }

    private static string Csv(params string[] fields)
        => string.Join(",", fields.Select(f =>
            f.Contains(',') || f.Contains('"')
                ? "\"" + f.Replace("\"", "\"\"") + "\""
                : f));
}
