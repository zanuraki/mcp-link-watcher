namespace Zanuraki.Mcp.LinkWatcher.Services.Sources;

public sealed record LinkSnapshot(
    string? Title,
    string Status,
    IReadOnlyDictionary<string, string> Fingerprint,
    IReadOnlyList<SourceComment> Comments);
