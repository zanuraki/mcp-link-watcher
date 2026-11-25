using Zanuraki.Mcp.LinkWatcher.Commands;
using Zanuraki.Mcp.LinkWatcher.Hosting;

using var host = HostFactory.Create(args);

return await CommandLineRoot.Build(host).Parse(args).InvokeAsync();
