using Portfolio.Application.Common.Models;
using Portfolio.Infrastructure.Persistence.Repositories;
using Portfolio.IntegrationTests.Infrastructure;
using Xunit;

namespace Portfolio.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class ProfileAboutRepositoryTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task PublicProfileProjectsRequestedLocaleAndPublishedSocialLinksOnly()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var profile = CreateProfile();
        context.Profiles.Add(profile);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new ProfileRepository(context);

        var result = await repository.GetPublicAsync(
            "nam", "vi", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Ky su", result.Title);
        Assert.Equal("github", Assert.Single(result.SocialLinks).Platform);
    }

    [Fact]
    public async Task PublicAboutExcludesDraft()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var profile = CreateProfile();
        profile.About = new Portfolio.Application.Common.Models.About
        {
            IsPublished = false,
            Translations = { new AboutTranslation { LocaleCode = "en", Content = "About" } },
        };
        context.Profiles.Add(profile);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new AboutRepository(context);

        var result = await repository.GetPublicAsync(
            "nam", "en", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    private static Profile CreateProfile()
    {
        var profile = new Profile { Slug = "nam", FullName = "Nam" };
        profile.Translations.Add(new ProfileTranslation { LocaleCode = "en", Title = "Engineer" });
        profile.Translations.Add(new ProfileTranslation { LocaleCode = "vi", Title = "Ky su" });
        profile.SocialLinks.Add(new SocialLink
        {
            Platform = "github",
            Url = "https://github.com/example",
            IsPublished = true,
        });
        profile.SocialLinks.Add(new SocialLink
        {
            Platform = "draft",
            Url = "https://example.com/draft",
            IsPublished = false,
            DisplayOrder = 1,
        });
        return profile;
    }
}
