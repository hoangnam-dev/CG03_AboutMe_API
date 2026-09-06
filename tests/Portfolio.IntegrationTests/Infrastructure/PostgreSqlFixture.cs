using Microsoft.EntityFrameworkCore;
using Portfolio.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Portfolio.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public PostgreSqlFixture()
    {
        try
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("portfolio_tests")
                .WithUsername("portfolio_tests")
                .WithPassword("portfolio-tests-only")
                .Build();
        }
        catch (Exception exception)
        {
            throw DockerRequired(exception);
        }
    }

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
        }
        catch (Exception exception)
        {
            throw DockerRequired(exception);
        }

        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public PortfolioDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new PortfolioDbContext(options);
    }

    public async Task ResetApplicationDataAsync(CancellationToken cancellationToken)
    {
        await using var context = CreateDbContext();
        await context.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                about_translations,
                abouts,
                certificate_technologies,
                certificate_translations,
                certificates,
                contact_messages,
                experience_highlights,
                experience_technologies,
                experience_translations,
                project_highlights,
                project_image_translations,
                project_images,
                project_technologies,
                project_translations,
                projects,
                resume_translations,
                resumes,
                resume_version_counters,
                site_settings,
                technology_translations,
                technologies,
                skill_category_translations,
                skill_categories,
                social_links,
                profile_translations,
                profiles
            RESTART IDENTITY CASCADE;
            """,
            cancellationToken);
    }

    private static InvalidOperationException DockerRequired(Exception exception) =>
        new(
            "PostgreSQL integration tests require a running Docker engine and access to the pinned postgres:17-alpine image.",
            exception);
}
