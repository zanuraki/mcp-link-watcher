namespace Zanuraki.Mcp.LinkWatcher.Models;

public sealed record PendingComment(string Author, DateTimeOffset CreatedAt, string Body);
