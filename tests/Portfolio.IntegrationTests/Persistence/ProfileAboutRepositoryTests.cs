using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Common.Storage;
using Portfolio.Application.Profiles;
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

    [Fact]
    public async Task UpdateExistingProfileInsertsFirstSocialLink()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using (var seedContext = database.CreateDbContext())
        {
            seedContext.Profiles.Add(CreateProfileWithoutSocialLinks());
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateDbContext();
        var repository = new ProfileRepository(context);
        var storage = new StorageStub();
        var service = new ProfileService(
            repository,
            storage,
            new StorageReplacement(storage, NullLogger<StorageReplacement>.Instance),
            new ProfileMediaSettings("avatars", 1024),
            TimeProvider.System,
            NullLogger<ProfileService>.Instance);
        var request = new ProfileUpdateRequest(
            "nam",
            "Nam",
            null,
            null,
            false,
            false,
            true,
            new Dictionary<string, ProfileTranslationRequest>
            {
                ["en"] = new("Engineer", null, null, null),
                ["vi"] = new("Ky su", null, null, null),
            },
            [new("github", "GitHub", "https://github.com/example", "github", 0, true)]);

        await service.UpdateAsync(request, TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();
        var link = await context.SocialLinks.AsNoTracking().SingleAsync(
            TestContext.Current.CancellationToken);
        Assert.NotEqual(Guid.Empty, link.Id);
        Assert.Equal("github", link.Platform);
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

    private static Profile CreateProfileWithoutSocialLinks()
    {
        var profile = new Profile { Slug = "nam", FullName = "Nam" };
        profile.Translations.Add(new ProfileTranslation { LocaleCode = "en", Title = "Engineer" });
        profile.Translations.Add(new ProfileTranslation { LocaleCode = "vi", Title = "Ky su" });
        return profile;
    }

    private sealed class StorageStub : IFileStorage
    {
        public Task<StorageObject> UploadAsync(StorageUpload upload, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteIfExistsAsync(
            string bucket,
            string objectKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Uri GetPublicReadUrl(string bucket, string objectKey) =>
            new($"https://storage.example/{bucket}/{objectKey}");

        public Task<Uri> CreateSignedReadUrlAsync(
            string bucket,
            string objectKey,
            TimeSpan lifetime,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
