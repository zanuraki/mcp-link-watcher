namespace Zanuraki.Mcp.LinkWatcher.Models;

public sealed class LinkState
{
    public required string Id { get; set; }

    public Dictionary<string, string>? Fingerprint { get; set; }

    public string? LastSummary { get; set; }

    public string? LastStatus { get; set; }

    public DateTimeOffset? LastChecked { get; set; }

    public string? LastFetchedVia { get; set; }

    public string? PendingDelta { get; set; }
}
