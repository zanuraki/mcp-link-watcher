namespace Zanuraki.Mcp.LinkWatcher.Commands;

using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Zanuraki.Mcp.LinkWatcher.Configuration;

public static class CommandLineRoot
{
    public static RootCommand Build(IHost host)
    {
        return new RootCommand("link-watcher MCP server — watches URLs from bug reports, support issues and feature requests")
        {
            McpCommand.Create(new McpCommand(host)),
            SettingsCommand.Create(host.Services.GetRequiredService<IOptions<LinkWatcherOptions>>()),
        };
    }
}
