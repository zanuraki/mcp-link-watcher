namespace Zanuraki.Mcp.LinkWatcher.Services.Sources;

public sealed class SourceResolver(IEnumerable<ILinkSource> sources)
{
    public ILinkSource? Resolve(Uri url) => sources.FirstOrDefault(source => source.CanHandle(url));
}
