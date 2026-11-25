namespace Zanuraki.Mcp.LinkWatcher.Services.Sources;

public sealed record LinkDelta(
    string? PreviousStatus,
    string CurrentStatus,
    bool StatusChanged,
    IReadOnlyList<SourceComment> NewComments);
