using Microsoft.Extensions.Configuration;
using Portfolio.Api.Logging;
using Serilog;
using Xunit;

namespace Portfolio.IntegrationTests.Infrastructure;

public sealed class FileLoggingTests
{
    [Fact]
    public void WritesRollingLogAndDeletesFilesOlderThanSevenDays()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "portfolio-file-logging-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "portfolio-.log");
            var expiredPath = Path.Combine(
                directory,
                $"portfolio-{DateTime.UtcNow.AddDays(-8):yyyyMMdd}.log");
            File.WriteAllText(expiredPath, "expired");
            File.SetLastWriteTimeUtc(expiredPath, DateTime.UtcNow.AddDays(-8));
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Serilog:FilePath"] = path,
                })
                .Build();
            var marker = $"request-{Guid.NewGuid():N}";

            using (var logger = PortfolioFileLogging
                .Configure(new LoggerConfiguration(), configuration)
                .CreateLogger())
            {
                logger.ForContext("RequestId", marker).Information("Handled request");
            }

            Assert.False(File.Exists(expiredPath));
            var currentLog = Assert.Single(Directory.GetFiles(directory, "portfolio-*.log"));
            Assert.Contains(marker, File.ReadAllText(currentLog), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
