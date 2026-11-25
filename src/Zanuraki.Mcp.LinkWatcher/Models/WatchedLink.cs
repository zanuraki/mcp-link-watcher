namespace Zanuraki.Mcp.LinkWatcher.Models;

public sealed record WatchedLink
{
    public required string Id { get; init; }

    public required Uri Url { get; init; }

    public required string Title { get; init; }

    public required LinkKind Kind { get; init; }

    public required DateOnly Added { get; init; }

    public IReadOnlyList<string> Related { get; init; } = [];

    public required string Why { get; init; }

    public required string ResolvedWhen { get; init; }

    public string? Workaround { get; init; }

    public IReadOnlyList<LinkUpdate> Updates { get; init; } = [];
}
