namespace Zanuraki.Mcp.LinkWatcher.Models;

/// <summary>
/// A change detected on a tracker link that the agent has not yet acknowledged. Serialized into
/// <see cref="LinkState.PendingDelta"/>; <see cref="LinkState.Fingerprint"/> is only advanced when it is acknowledged.
/// </summary>
public sealed record PendingDelta(
    string? PreviousStatus,
    string CurrentStatus,
    IReadOnlyList<PendingComment> NewComments,
    IReadOnlyDictionary<string, string> NewFingerprint);
