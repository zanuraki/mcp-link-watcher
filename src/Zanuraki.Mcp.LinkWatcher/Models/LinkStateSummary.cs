namespace Zanuraki.Mcp.LinkWatcher.Models;

public sealed record LinkStateSummary(
    IReadOnlyDictionary<string, string>? Fingerprint,
    string? LastStatus,
    string? LastSummary,
    DateTimeOffset? LastChecked,
    string? LastFetchedVia,
    bool HasPendingChange);
