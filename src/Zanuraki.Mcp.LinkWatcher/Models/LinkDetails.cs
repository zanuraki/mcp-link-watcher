namespace Zanuraki.Mcp.LinkWatcher.Models;

public sealed record LinkDetails(
    string Id,
    string Url,
    string Title,
    string Kind,
    DateOnly Added,
    string CheckedBy,
    IReadOnlyList<string> Related,
    string Why,
    string ResolvedWhen,
    string? Workaround,
    IReadOnlyList<LinkUpdate> Updates,
    LinkStateSummary? State);
