namespace Zanuraki.Mcp.LinkWatcher;

using Microsoft.Extensions.Logging;
using Serilog;

internal static class LoggingBuilderExtensions
{
    internal static ILoggingBuilder ConfigureSerilog(this ILoggingBuilder logging)
    {
        var logger = new LoggerConfiguration()
                     .MinimumLevel.Debug()
                     .WriteTo.File(
                         Constants.LogFilePath,
                         rollingInterval: RollingInterval.Day,
                         retainedFileCountLimit: 7,
                         shared: true,
                         outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                     .CreateLogger();

        logging.ClearProviders();
        logging.AddSerilog(logger, dispose: true);

        return logging;
    }
}
