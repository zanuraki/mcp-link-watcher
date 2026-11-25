namespace Zanuraki.Mcp.LinkWatcher.Services.Sources;

public interface ILinkSource
{
    string Name { get; }

    bool CanHandle(Uri url);

    /// <exception cref="HttpRequestException">The tracker API answered with a non-success status.</exception>
    Task<LinkSnapshot> FetchAsync(Uri url, CancellationToken cancellationToken);
}
