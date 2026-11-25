namespace Zanuraki.Mcp.LinkWatcher;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zanuraki.Mcp.LinkWatcher.Configuration;
using Zanuraki.Mcp.LinkWatcher.Filters;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection ConfigureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LinkWatcherOptions>(configuration.GetSection(LinkWatcherOptions.SectionName));
        services.AddHttpClient();

        services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly()
            .WithRequestFilters(filters => filters.AddToolTimeoutFilter(TimeSpan.FromSeconds(60)));

        return services;
    }
}
