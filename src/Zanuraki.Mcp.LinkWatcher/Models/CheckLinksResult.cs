namespace Zanuraki.Mcp.LinkWatcher.Models;

public sealed record CheckLinksResult(
    IReadOnlyList<ChangedLink> Changed,
    IReadOnlyList<AgentCheckItem> NeedsAgentCheck,
    IReadOnlyList<LinkError> Errors,
    int UnchangedCount,
    int BaselinedCount,
    string Note);
