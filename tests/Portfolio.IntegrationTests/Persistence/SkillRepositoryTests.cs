using Portfolio.Application.Common.Models;
using Portfolio.Application.Skills;
using Portfolio.Infrastructure.Persistence.Repositories;
using Portfolio.IntegrationTests.Infrastructure;
using Xunit;

namespace Portfolio.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class SkillRepositoryTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task PersistsLucideInsteadOfApplyingTextDatabaseDefault()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var category = Category("Backend", "May chu", 0, true);
        category.Technologies.Add(Skill("PostgreSQL", "PostgreSQL", 0, true));
        context.SkillCategories.Add(category);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        Assert.Equal(
            SkillIconType.Lucide,
            context.Technologies.Select(skill => skill.IconType).Single());
    }

    [Fact]
    public async Task PublicGroupingFiltersDraftsAndOrdersInRequestedLocale()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        context.Profiles.Add(Profile());
        var later = Category("Backend", "May chu", 2, true);
        var earlier = Category("Frontend", "Giao dien", 1, true);
        var draftCategory = Category("Draft", "Nhap", 0, false);
        later.Technologies.Add(Skill("PostgreSQL", "PostgreSQL", 1, true));
        later.Technologies.Add(Skill("Draft skill", "Ky nang nhap", 0, false));
        earlier.Technologies.Add(Skill("React", "React", 0, true));
        draftCategory.Technologies.Add(Skill("Hidden", "An", 0, true));
        context.SkillCategories.AddRange(later, earlier, draftCategory);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new SkillRepository(context);

        var result = await repository.GetPublicAsync("nam", "vi", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(["Giao dien", "May chu"], result.Select(category => category.Name));
        Assert.Equal("PostgreSQL", Assert.Single(result[1].Skills).Name);
    }

    [Fact]
    public async Task AdminListSearchesLocalizedNamesAndPaginates()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var category = Category("Backend", "May chu", 0, true);
        category.Technologies.Add(Skill("PostgreSQL", "Co so du lieu", 1, true));
        category.Technologies.Add(Skill("ASP.NET Core", "ASP.NET Core", 0, false));
        context.SkillCategories.Add(category);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new SkillRepository(context);

        var result = await repository.GetSkillsAsync(
            new SkillAdminQuery(1, 20, "co so", category.Id, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Total);
        Assert.Equal("PostgreSQL", Assert.Single(result.Items).Translations["en"].Name);
    }

    [Fact]
    public async Task ReorderRejectsPartialSetWithoutChangingOrders()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var category = Category("Backend", "May chu", 0, true);
        var first = Skill("A", "A", 0, true);
        var second = Skill("B", "B", 1, true);
        category.Technologies.Add(first);
        category.Technologies.Add(second);
        context.SkillCategories.Add(category);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new SkillRepository(context);

        var result = await repository.ReorderSkillsAsync(
            category.Id, [new SkillOrderItem(first.Id, 9)], TestContext.Current.CancellationToken);

        Assert.Equal(SkillReorderResult.SetMismatch, result);
        context.ChangeTracker.Clear();
        var orders = context.Technologies.OrderBy(item => item.DisplayOrder)
            .Select(item => item.DisplayOrder).ToArray();
        Assert.Equal([0, 1], orders);
    }

    private static Profile Profile()
    {
        var profile = new Profile { Slug = "nam", FullName = "Nam" };
        profile.Translations.Add(new ProfileTranslation { LocaleCode = "en", Title = "Engineer" });
        profile.Translations.Add(new ProfileTranslation { LocaleCode = "vi", Title = "Ky su" });
        return profile;
    }

    private static SkillCategory Category(string en, string vi, int order, bool published)
    {
        var category = new SkillCategory { DisplayOrder = order, IsPublished = published };
        category.Translations.Add(new SkillCategoryTranslation { LocaleCode = "en", Name = en });
        category.Translations.Add(new SkillCategoryTranslation { LocaleCode = "vi", Name = vi });
        return category;
    }

    private static Technology Skill(string en, string vi, int order, bool published)
    {
        var skill = new Technology
        {
            SkillLevel = SkillLevel.Primary,
            IconType = SkillIconType.Lucide,
            IconValue = "database",
            DisplayOrder = order,
            IsPublished = published,
        };
        skill.Translations.Add(new TechnologyTranslation { LocaleCode = "en", Name = en });
        skill.Translations.Add(new TechnologyTranslation { LocaleCode = "vi", Name = vi });
        return skill;
    }
}
