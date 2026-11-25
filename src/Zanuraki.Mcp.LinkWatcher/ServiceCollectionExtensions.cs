namespace Zanuraki.Mcp.LinkWatcher;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zanuraki.Mcp.LinkWatcher.Configuration;
using Zanuraki.Mcp.LinkWatcher.Filters;
using Zanuraki.Mcp.LinkWatcher.Services;
using Zanuraki.Mcp.LinkWatcher.Services.Sources;
using Zanuraki.Mcp.LinkWatcher.Tools;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection ConfigureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LinkWatcherOptions>(configuration.GetSection(LinkWatcherOptions.SectionName));
        services.AddHttpClient(string.Empty, client => client.Timeout = TimeSpan.FromSeconds(20));
        services.AddSingleton<FrontmatterService>();
        services.AddSingleton<LinkStore>();
        services.AddSingleton<ILinkSource, GitHubIssueSource>();
        services.AddSingleton<ILinkSource, BugzillaSource>();
        services.AddSingleton<SourceResolver>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<LinkTools>();

        services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly()
            .WithRequestFilters(filters => filters.AddToolTimeoutFilter(TimeSpan.FromSeconds(60)));

        return services;
    }
}
