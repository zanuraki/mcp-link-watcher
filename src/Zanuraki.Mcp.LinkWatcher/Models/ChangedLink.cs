namespace Zanuraki.Mcp.LinkWatcher.Models;

public sealed record ChangedLink(
    string Id,
    string Url,
    string Title,
    string ResolvedWhen,
    string? PreviousStatus,
    string CurrentStatus,
    IReadOnlyList<PendingComment> NewComments);
