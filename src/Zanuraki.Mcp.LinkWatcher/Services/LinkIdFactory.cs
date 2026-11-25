namespace Zanuraki.Mcp.LinkWatcher.Services;

using System.Text.RegularExpressions;
using System.Web;

public static partial class LinkIdFactory
{
    private const int MaxLength = 80;

    /// <summary>Derives the stable file-name id for a watched URL. The same URL always yields the same id.</summary>
    public static string Create(Uri url)
    {
        var id = GitHubId(url) ?? BugzillaId(url) ?? $"{url.Host}-{url.AbsolutePath}{url.Query}";
        return Slugify(id);
    }

    private static string? GitHubId(Uri url)
    {
        if (url.Host is not ("github.com" or "www.github.com"))
        {
            return null;
        }

        var parts = url.AbsolutePath.Trim('/').Split('/');
        if (parts.Length != 4 || !int.TryParse(parts[3], out _))
        {
            return null;
        }

        var (owner, repo, section, number) = (parts[0], parts[1], parts[2], parts[3]);
        return section switch
        {
            "issues" or "pull" => $"github-{owner}-{repo}-{number}",
            "discussions" => $"github-{owner}-{repo}-discussion-{number}",
            _ => null,
        };
    }

    private static string? BugzillaId(Uri url)
    {
        if (!url.AbsolutePath.EndsWith("show_bug.cgi", StringComparison.Ordinal))
        {
            return null;
        }

        var id = HttpUtility.ParseQueryString(url.Query)["id"];
        return id is not null && int.TryParse(id, out _)
            ? $"{url.Host.Replace('.', '-')}-{id}"
            : null;
    }

    private static string Slugify(string value)
    {
        var slug = NonAlphanumeric().Replace(value.ToLowerInvariant(), "-").Trim('-');
        if (slug.Length > MaxLength)
        {
            slug = slug[..MaxLength].TrimEnd('-');
        }

        return slug.StartsWith("www-", StringComparison.Ordinal) ? slug[4..] : slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
