namespace Zanuraki.Mcp.LinkWatcher.Models;

public sealed record AgentCheckItem(
    string Id,
    string Url,
    string Title,
    string ResolvedWhen,
    string? LastSummary,
    string? LastFetchedVia,
    DateTimeOffset? LastChecked);
