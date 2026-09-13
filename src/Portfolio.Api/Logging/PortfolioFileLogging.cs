using Serilog;

namespace Portfolio.Api.Logging;

public static class PortfolioFileLogging
{
    private const string DefaultPath = "logs/portfolio-.log";
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{RequestId}] " +
        "{SourceContext} {Message:lj}{NewLine}{Exception}";
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    public static LoggerConfiguration Configure(
        LoggerConfiguration logger,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(configuration);

        var path = configuration["Serilog:FilePath"];
        if (string.IsNullOrWhiteSpace(path)) path = DefaultPath;

        return logger.WriteTo.File(
            path,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: null,
            retainedFileTimeLimit: Retention,
            rollOnFileSizeLimit: true,
            fileSizeLimitBytes: 100 * 1024 * 1024,
            shared: true,
            outputTemplate: OutputTemplate,
            formatProvider: System.Globalization.CultureInfo.InvariantCulture);
    }
}
