using Portfolio.Application.Common.Models;
using Portfolio.Application.Experiences;
using Portfolio.Infrastructure.Persistence.Repositories;
using Portfolio.IntegrationTests.Infrastructure;
using Xunit;

namespace Portfolio.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class ExperienceRepositoryTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task PublicListFiltersDraftsAndOrdersByDisplayOrderThenStartDate()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        context.Profiles.Add(Profile());
        context.WorkExperiences.AddRange(
            Experience("First", new DateOnly(2022, 1, 1), 0, true),
            Experience("Newer", new DateOnly(2024, 1, 1), 1, true),
            Experience("Older", new DateOnly(2023, 1, 1), 1, true),
            Experience("Draft", new DateOnly(2025, 1, 1), 0, false));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new ExperienceRepository(context);

        var result = await repository.GetPublicAsync(
            "nam", "en", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(["First", "Newer", "Older"], result.Select(item => item.CompanyName));
        Assert.DoesNotContain(result, item => item.CompanyName == "Draft");
    }

    [Fact]
    public async Task PublicListReturnsRequestedLocaleAndPublishedTechnologiesOnly()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        context.Profiles.Add(Profile());
        var category = Category();
        var published = Technology(category, "PostgreSQL", "PostgreSQL", true);
        var draft = Technology(category, "Internal", "Noi bo", false);
        var experience = Experience("Example", new DateOnly(2024, 1, 1), 0, true);
        experience.Highlights.Add(new ExperienceHighlight
        {
            LocaleCode = "en",
            HighlightType = ExperienceHighlightType.Achievement,
            Content = "English",
            DisplayOrder = 0,
        });
        experience.Highlights.Add(new ExperienceHighlight
        {
            LocaleCode = "vi",
            HighlightType = ExperienceHighlightType.Achievement,
            Content = "Tieng Viet",
            DisplayOrder = 0,
        });
        experience.Technologies.Add(new ExperienceTechnology
        {
            Technology = draft,
            TechnologyId = draft.Id,
            DisplayOrder = 0,
        });
        experience.Technologies.Add(new ExperienceTechnology
        {
            Technology = published,
            TechnologyId = published.Id,
            DisplayOrder = 1,
        });
        context.WorkExperiences.Add(experience);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new ExperienceRepository(context);

        var result = await repository.GetPublicAsync(
            "nam", "vi", TestContext.Current.CancellationToken);

        var item = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<PublicExperienceResponse>>(result));
        Assert.Equal("Ky su", item.Translation.Position);
        Assert.Equal("Tieng Viet", Assert.Single(item.Highlights).Content);
        Assert.Equal("PostgreSQL", Assert.Single(item.Technologies).Name);
        Assert.True(item.IsCurrent);
    }

    [Fact]
    public async Task AdminListSearchesCompanyAndLocalizedPosition()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        context.WorkExperiences.AddRange(
            Experience("Example", new DateOnly(2024, 1, 1), 0, true),
            Experience("Other", new DateOnly(2023, 1, 1), 1, false, "Platform Engineer", "Ky su nen tang"));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new ExperienceRepository(context);

        var byPosition = await repository.GetExperiencesAsync(
            new ExperienceAdminQuery(1, 20, "nen tang", null),
            TestContext.Current.CancellationToken);
        var byCompany = await repository.GetExperiencesAsync(
            new ExperienceAdminQuery(1, 20, "exam", true),
            TestContext.Current.CancellationToken);

        Assert.Equal("Other", Assert.Single(byPosition.Items).CompanyName);
        Assert.Equal("Example", Assert.Single(byCompany.Items).CompanyName);
    }

    [Fact]
    public async Task AggregateSavePersistsTranslationsHighlightsAndOrderedTechnologies()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var category = Category();
        var first = Technology(category, "One", "Mot", true);
        var second = Technology(category, "Two", "Hai", true);
        context.Technologies.AddRange(first, second);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var experience = Experience("Example", new DateOnly(2024, 1, 1), 0, true);
        experience.Highlights.Add(new ExperienceHighlight
        {
            LocaleCode = "en",
            HighlightType = ExperienceHighlightType.Responsibility,
            Content = "Built APIs",
            DisplayOrder = 0,
        });
        experience.Technologies.Add(new ExperienceTechnology
        {
            TechnologyId = second.Id,
            DisplayOrder = 0,
        });
        experience.Technologies.Add(new ExperienceTechnology
        {
            TechnologyId = first.Id,
            DisplayOrder = 1,
        });
        var repository = new ExperienceRepository(context);

        await repository.AddAsync(experience, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var stored = await repository.GetAsync(
            experience.Id, false, TestContext.Current.CancellationToken);

        Assert.NotNull(stored);
        Assert.Equal(2, stored.Translations.Count);
        Assert.Equal("Built APIs", Assert.Single(stored.Highlights).Content);
        Assert.Equal(
            [second.Id, first.Id],
            stored.Technologies.OrderBy(item => item.DisplayOrder).Select(item => item.TechnologyId));
    }

    [Fact]
    public async Task ReorderRejectsPartialSetWithoutChangingOrders()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var first = Experience("First", new DateOnly(2024, 1, 1), 0, true);
        var second = Experience("Second", new DateOnly(2023, 1, 1), 1, true);
        context.WorkExperiences.AddRange(first, second);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new ExperienceRepository(context);

        var result = await repository.ReorderAsync(
            [new ExperienceOrderItem(first.Id, 9)], TestContext.Current.CancellationToken);

        Assert.Equal(ExperienceReorderResult.SetMismatch, result);
        context.ChangeTracker.Clear();
        Assert.Equal(
            [0, 1],
            context.WorkExperiences.OrderBy(item => item.DisplayOrder).Select(item => item.DisplayOrder));
    }

    private static Profile Profile()
    {
        var profile = new Profile { Slug = "nam", FullName = "Nam" };
        profile.Translations.Add(new ProfileTranslation { LocaleCode = "en", Title = "Engineer" });
        profile.Translations.Add(new ProfileTranslation { LocaleCode = "vi", Title = "Ky su" });
        return profile;
    }

    private static WorkExperience Experience(
        string company,
        DateOnly start,
        int order,
        bool published,
        string enPosition = "Engineer",
        string viPosition = "Ky su")
    {
        var experience = new WorkExperience
        {
            CompanyName = company,
            StartDate = start,
            DisplayOrder = order,
            IsPublished = published,
        };
        experience.Translations.Add(new ExperienceTranslation
        {
            LocaleCode = "en",
            Position = enPosition,
            Location = "Remote",
        });
        experience.Translations.Add(new ExperienceTranslation
        {
            LocaleCode = "vi",
            Position = viPosition,
            Location = "Tu xa",
        });
        return experience;
    }

    private static SkillCategory Category()
    {
        var category = new SkillCategory { DisplayOrder = 0, IsPublished = true };
        category.Translations.Add(new SkillCategoryTranslation { LocaleCode = "en", Name = "Backend" });
        category.Translations.Add(new SkillCategoryTranslation { LocaleCode = "vi", Name = "May chu" });
        return category;
    }

    private static Technology Technology(
        SkillCategory category, string en, string vi, bool published)
    {
        var technology = new Technology
        {
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
