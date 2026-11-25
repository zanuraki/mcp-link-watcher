namespace Zanuraki.Mcp.LinkWatcher.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class HostFactory
{
    public static IHost Create(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.Configure<ConsoleLifetimeOptions>(o => o.SuppressStatusMessages = true);

        builder.Configuration.AddJsonFile(Constants.AppSettingsFilePath, optional: true, reloadOnChange: false);
        builder.Logging.ConfigureSerilog();
        builder.Services.ConfigureServices(builder.Configuration);

        return builder.Build();
    }
}
