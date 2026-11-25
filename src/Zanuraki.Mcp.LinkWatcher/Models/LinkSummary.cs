namespace Zanuraki.Mcp.LinkWatcher.Models;

public sealed record LinkSummary(
    string Id,
    string Url,
    string Title,
    string Kind,
    DateOnly Added,
    string CheckedBy,
    string? LastStatus,
    DateTimeOffset? LastChecked,
    string? LastFetchedVia);
