using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.Infrastructure;
using Portfolio.Infrastructure.Persistence;
using Xunit;

namespace Portfolio.IntegrationTests.Infrastructure;

public sealed class DatabaseBatchingTests
{
    [Fact]
    public void InfrastructureUsesSingleStatementModificationBatches()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] =
                    "Host=localhost;Database=portfolio;Username=portfolio;Password=test",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration, allowUnconfiguredDependencies: true);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var options = scope.ServiceProvider
            .GetRequiredService<DbContextOptions<PortfolioDbContext>>();
        var relationalOptions = Assert.Single(
            options.Extensions.OfType<RelationalOptionsExtension>());

        Assert.Equal(1, relationalOptions.MaxBatchSize);
    }
}
