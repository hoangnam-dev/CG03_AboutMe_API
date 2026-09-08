using Portfolio.Application.Certificates;
using Portfolio.Application.Common.Models;
using Portfolio.Infrastructure.Persistence.Repositories;
using Portfolio.IntegrationTests.Infrastructure;
using Xunit;

namespace Portfolio.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class CertificateRepositoryTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task PublicListFiltersDraftsAndOrdersByDisplayOrderThenIssuedDate()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        context.Profiles.Add(Profile());
        context.Certificates.AddRange(
            Certificate("First", new DateOnly(2024, 1, 1), 0, true),
            Certificate("Newer", new DateOnly(2026, 1, 1), 1, true),
            Certificate("Older", new DateOnly(2025, 1, 1), 1, true),
            Certificate("Draft", new DateOnly(2026, 1, 1), 0, false));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new CertificateRepository(context);

        var result = await repository.GetPublicAsync(
            "nam", "en", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(["First", "Newer", "Older"], result.Select(item => item.Name));
        Assert.DoesNotContain(result, item => item.Name == "Draft");
    }

    [Fact]
    public async Task PublicListReturnsExactLocalePublishedTechnologiesAndPrivateObjectKey()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        context.Profiles.Add(Profile());
        var category = Category();
        var published = Technology(category, "PostgreSQL", "PostgreSQL", true);
        var draft = Technology(category, "Internal", "Noi bo", false);
        var certificate = Certificate("Cloud", new DateOnly(2026, 1, 1), 0, true);
        certificate.FileUrl = "certificates/id/evidence.pdf";
        certificate.Technologies.Add(new CertificateTechnology { Technology = draft, TechnologyId = draft.Id });
        certificate.Technologies.Add(new CertificateTechnology { Technology = published, TechnologyId = published.Id });
        context.Certificates.Add(certificate);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new CertificateRepository(context);

        var result = await repository.GetPublicAsync(
            "nam", "vi", TestContext.Current.CancellationToken);

        var item = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<CertificatePublicProjection>>(result));
        Assert.Equal("Cloud vi", item.Name);
        Assert.Equal("certificates/id/evidence.pdf", item.FileObjectKey);
        Assert.Null(item.ImageObjectKey);
        Assert.Equal("PostgreSQL", Assert.Single(item.Technologies).Name);
    }

    [Fact]
    public async Task AggregateSavePersistsTranslationsAndTechnologies()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var category = Category();
        var technology = Technology(category, "PostgreSQL", "PostgreSQL", true);
        context.Technologies.Add(technology);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var certificate = Certificate("Cloud", new DateOnly(2026, 1, 1), 0, true);
        certificate.Technologies.Add(new CertificateTechnology { TechnologyId = technology.Id });
        var repository = new CertificateRepository(context);

        await repository.AddAsync(certificate, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var stored = await repository.GetAsync(
            certificate.Id, false, TestContext.Current.CancellationToken);

        Assert.NotNull(stored);
        Assert.Equal(2, stored.Translations.Count);
        Assert.Equal(technology.Id, Assert.Single(stored.Technologies).TechnologyId);
    }

    private static Profile Profile()
    {
        var profile = new Profile { Slug = "nam", FullName = "Nam" };
        profile.Translations.Add(new ProfileTranslation { LocaleCode = "en", Title = "Engineer" });
        profile.Translations.Add(new ProfileTranslation { LocaleCode = "vi", Title = "Ky su" });
        return profile;
    }

    private static Certificate Certificate(string name, DateOnly issued, int order, bool published)
    {
        var certificate = new Certificate
        {
            Issuer = "Authority",
            IssuedDate = issued,
            DisplayOrder = order,
            IsPublished = published,
        };
        certificate.Translations.Add(new CertificateTranslation { LocaleCode = "en", Name = name });
        certificate.Translations.Add(new CertificateTranslation { LocaleCode = "vi", Name = $"{name} vi" });
        return certificate;
    }

    private static SkillCategory Category()
    {
        var category = new SkillCategory { DisplayOrder = 0, IsPublished = true };
        category.Translations.Add(new SkillCategoryTranslation { LocaleCode = "en", Name = "Backend" });
        category.Translations.Add(new SkillCategoryTranslation { LocaleCode = "vi", Name = "May chu" });
        return category;
    }

    private static Technology Technology(SkillCategory category, string en, string vi, bool published)
    {
        var technology = new Technology
        {
            Id = Guid.NewGuid(),
            Category = category,
            SkillLevel = SkillLevel.Primary,
            IconType = SkillIconType.Lucide,
            IconValue = "database",
            DisplayOrder = category.Technologies.Count,
            IsPublished = published,
        };
        technology.Translations.Add(new TechnologyTranslation { LocaleCode = "en", Name = en });
        technology.Translations.Add(new TechnologyTranslation { LocaleCode = "vi", Name = vi });
        category.Technologies.Add(technology);
        return technology;
    }
}
