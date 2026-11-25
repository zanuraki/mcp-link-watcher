namespace Zanuraki.Mcp.LinkWatcher.Services.Sources;

public sealed record SourceComment(string Id, string Author, DateTimeOffset CreatedAt, string Body);
