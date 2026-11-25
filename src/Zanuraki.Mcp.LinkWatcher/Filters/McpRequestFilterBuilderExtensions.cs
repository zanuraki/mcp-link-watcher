namespace Zanuraki.Mcp.LinkWatcher.Filters;

using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

internal static class McpRequestFilterBuilderExtensions
{
    internal static void AddToolTimeoutFilter(this IMcpRequestFilterBuilder filters, TimeSpan timeout)
    {
        filters.AddCallToolFilter(next => async (context, cancellationToken) =>
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            try
            {
                return await next(context, cts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested == false)
            {
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = $"Tool '{context.Params.Name}' timed out" }],
                    IsError = true,
                };
            }
        });
    }
}
