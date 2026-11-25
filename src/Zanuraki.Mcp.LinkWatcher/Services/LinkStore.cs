namespace Zanuraki.Mcp.LinkWatcher.Services;

using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Zanuraki.Mcp.LinkWatcher.Configuration;
using Zanuraki.Mcp.LinkWatcher.Models;

public sealed class LinkStore
{
    private const string DateFormat = "yyyy-MM-dd";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly FrontmatterService _frontmatter;
    private readonly string _dataPath;
    private readonly string _statePath;

    public LinkStore(IOptions<LinkWatcherOptions> options, FrontmatterService frontmatter)
    {
        _frontmatter = frontmatter;
        _dataPath = ExpandHome(options.Value.DataPath);
        _statePath = ExpandHome(options.Value.StatePath);
    }

    public IReadOnlyList<WatchedLink> GetAll()
    {
        if (!Directory.Exists(_dataPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(_dataPath)
            .Where(path => Path.GetExtension(path) == ".md")
            .Select(path => ParseLink(Path.GetFileNameWithoutExtension(path), File.ReadAllText(path)))
            .OrderBy(link => link.Added)
            .ThenBy(link => link.Id, StringComparer.Ordinal)
            .ToList();
    }

    public WatchedLink? Get(string id)
    {
        var path = MarkdownPath(id);
        return File.Exists(path) ? ParseLink(id, File.ReadAllText(path)) : null;
    }

    public WatchedLink? FindByUrl(Uri url)
    {
        var link = Get(LinkIdFactory.Create(url));
        return link?.Url == url ? link : null;
    }

    /// <summary>Writes the link atomically: the Markdown is written to a temporary file and moved over the target.</summary>
    public void Save(WatchedLink link)
    {
        Directory.CreateDirectory(_dataPath);
        WriteAtomically(MarkdownPath(link.Id), RenderLink(link));
    }

    /// <summary>Appends an update to the link's Markdown file, leaving all other fields untouched.</summary>
    public void AppendUpdate(string id, LinkUpdate update)
    {
        var link = Get(id) ?? throw new KeyNotFoundException($"No watched link with id '{id}'.");
        Save(link with { Updates = [.. link.Updates, update] });
    }

    /// <summary>Deletes the link's Markdown file and its state file. Returns false when no Markdown file exists.</summary>
    public bool Delete(string id)
    {
        var markdownPath = MarkdownPath(id);
        if (!File.Exists(markdownPath))
        {
            return false;
        }

        File.Delete(markdownPath);
        var statePath = StatePath(id);
        if (File.Exists(statePath))
        {
            File.Delete(statePath);
        }

        return true;
    }

    public LinkState? GetState(string id)
    {
        var path = StatePath(id);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<LinkState>(File.ReadAllText(path), JsonOptions)
            : null;
    }

    public void SaveState(LinkState state)
    {
        Directory.CreateDirectory(_statePath);
        WriteAtomically(StatePath(state.Id), JsonSerializer.Serialize(state, JsonOptions));
    }

    /// <summary>Deletes state files whose link has no Markdown file. Returns the number of files deleted.</summary>
    public int PruneOrphanedStates()
    {
        if (!Directory.Exists(_statePath))
        {
            return 0;
        }

        var orphans = Directory.EnumerateFiles(_statePath)
            .Where(path => Path.GetExtension(path) == ".json")
            .Where(path => !File.Exists(MarkdownPath(Path.GetFileNameWithoutExtension(path))))
            .ToList();

        foreach (var orphan in orphans)
        {
            File.Delete(orphan);
        }

        return orphans.Count;
    }

    private static string ExpandHome(string path) =>
        path.StartsWith("~/", StringComparison.Ordinal)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..])
            : path;

    private static void WriteAtomically(string path, string content)
    {
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, content);
        File.Move(tempPath, path, overwrite: true);
    }

    private string MarkdownPath(string id) => Path.Combine(_dataPath, id + ".md");

    private string StatePath(string id) => Path.Combine(_statePath, id + ".json");

    private WatchedLink ParseLink(string id, string markdown)
    {
        var (frontmatter, body) = _frontmatter.Parse<LinkFrontmatter>(markdown);
        var sections = ParseSections(body);

        return new WatchedLink
        {
            Id = id,
            Url = new Uri(frontmatter.Url),
            Title = frontmatter.Title,
            Kind = Enum.Parse<LinkKind>(frontmatter.Kind, ignoreCase: true),
            Added = DateOnly.ParseExact(frontmatter.Added, DateFormat, CultureInfo.InvariantCulture),
            Related = frontmatter.Related ?? [],
            Why = SectionText(sections, "Why"),
            ResolvedWhen = SectionText(sections, "Resolved when"),
            Workaround = sections.ContainsKey("Workaround") ? SectionText(sections, "Workaround") : null,
            Updates = ParseUpdates(sections.GetValueOrDefault("Updates")),
        };
    }

    private string RenderLink(WatchedLink link)
    {
        var frontmatter = new LinkFrontmatter
        {
            Url = link.Url.AbsoluteUri,
            Title = link.Title,
            Kind = link.Kind.ToString().ToLowerInvariant(),
            Added = link.Added.ToString(DateFormat, CultureInfo.InvariantCulture),
            Related = link.Related.Count == 0 ? null : link.Related.ToList(),
        };

        var body = new StringBuilder();
        AppendSection(body, "Why", link.Why);
        AppendSection(body, "Resolved when", link.ResolvedWhen);
        if (link.Workaround is not null)
        {
            AppendSection(body, "Workaround", link.Workaround);
        }

        body.Append("## Updates\n");
        foreach (var update in link.Updates)
        {
            body.Append("- ")
                .Append(update.Date.ToString(DateFormat, CultureInfo.InvariantCulture))
                .Append(": ")
                .Append(update.Text)
                .Append('\n');
        }

        return _frontmatter.Serialize(frontmatter, body.ToString());
    }

    private static void AppendSection(StringBuilder body, string heading, string text) =>
        body.Append("## ").Append(heading).Append('\n').Append(text).Append("\n\n");

    private static Dictionary<string, List<string>> ParseSections(string body)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        List<string>? current = null;
        foreach (var line in body.Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                current = [];
                sections[line[3..].Trim()] = current;
            }
            else
            {
                current?.Add(line);
            }
        }

        return sections;
    }

    private static string SectionText(Dictionary<string, List<string>> sections, string heading) =>
        sections.TryGetValue(heading, out var lines) ? string.Join('\n', lines).Trim() : string.Empty;

    /// <summary>Each "- " line starts an update; indented or unbulleted non-blank lines continue the previous one.</summary>
    private static List<LinkUpdate> ParseUpdates(List<string>? lines)
    {
        var entries = new List<string>();
        foreach (var line in lines ?? [])
        {
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                entries.Add(line[2..]);
            }
            else if (line.Trim().Length > 0 && entries.Count > 0)
            {
                entries[^1] += "\n" + line;
            }
        }

        return entries
            .Select(entry => new LinkUpdate(
                DateOnly.ParseExact(entry[..10], DateFormat, CultureInfo.InvariantCulture),
                entry[12..].Trim()))
            .ToList();
    }

    private sealed class LinkFrontmatter
    {
        public string Url { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string Added { get; set; } = string.Empty;

        public List<string>? Related { get; set; }
    }
}
