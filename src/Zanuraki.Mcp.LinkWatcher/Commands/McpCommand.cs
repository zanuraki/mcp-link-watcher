namespace Zanuraki.Mcp.LinkWatcher.Commands;

using System.CommandLine;
using Microsoft.Extensions.Hosting;

public sealed class McpCommand(IHost host)
{
    public static Command Create(McpCommand command)
    {
        var mcpCommand = new Command("mcp", "Start the MCP stdio server");

        mcpCommand.SetAction((_, cancellationToken) => command.RunAsync(cancellationToken));

        return mcpCommand;
    }

    private Task RunAsync(CancellationToken cancellationToken)
    {
        return host.RunAsync(cancellationToken);
    }
}
