using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Portfolio.Application.Common.Models;
using Portfolio.Infrastructure.Persistence;
using Xunit;

namespace Portfolio.IntegrationTests.Persistence;

public sealed class PersistenceModelTests
{
    private static readonly string[] ExpectedPortfolioTables =
    [
        "abouts",
        "about_translations",
        "certificates",
        "certificate_technologies",
        "certificate_translations",
        "contact_messages",
        "experience_highlights",
        "experience_technologies",
        "experience_translations",
        "profiles",
        "profile_translations",
        "projects",
        "project_highlights",
        "project_images",
        "project_image_translations",
        "project_technologies",
        "project_translations",
        "resumes",
        "resume_translations",
        "resume_version_counters",
        "site_settings",
        "skill_categories",
        "skill_category_translations",
        "social_links",
        "technologies",
        "technology_translations",
        "work_experiences",
    ];

    [Fact]
    public void ModelContainsEveryDocumentedPortfolioTable()
    {
        using var context = CreateContext();
        var tableNames = context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(table => table is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        Assert.Subset(tableNames, ExpectedPortfolioTables.ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public void ResumeModelEnforcesOneActiveResumePerLanguage()
    {
        using var context = CreateContext();
        var resume = context.Model.FindEntityType(typeof(ResumeFile));

        var index = Assert.Single(
            Assert.IsAssignableFrom<IReadOnlyEntityType>(resume).GetIndexes(),
            candidate => candidate.IsUnique &&
                candidate.Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(ResumeFile.LanguageCode)]));

        Assert.Equal("is_active", index.GetFilter());
    }

    [Fact]
    public void TechnologyModelHasIconValueCheckConstraint()
    {
        using var context = CreateContext();
        var technology = context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(Technology));

        var constraint = Assert.Single(
            Assert.IsAssignableFrom<IReadOnlyEntityType>(technology).GetCheckConstraints(),
            candidate => candidate.Name == "ck_technologies_icon_value");

        Assert.Contains("icon_type", constraint.Sql, StringComparison.Ordinal);
        Assert.Contains("icon_value", constraint.Sql, StringComparison.Ordinal);
    }

    private static PortfolioDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseNpgsql("Host=localhost;Database=portfolio_model;Username=test;Password=test")
            .Options;

        return new PortfolioDbContext(options);
    }
}
