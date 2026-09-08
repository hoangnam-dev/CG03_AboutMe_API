using Microsoft.EntityFrameworkCore;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Resumes;
using Portfolio.Infrastructure.Persistence.Repositories;
using Portfolio.IntegrationTests.Infrastructure;
using Xunit;

namespace Portfolio.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class ResumeRepositoryTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task ConcurrentUploadsAllocateUniqueMonotonicSequences()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        const int count = 8;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, count).Select(async index =>
        {
            await start.Task;
            await using var context = database.CreateDbContext();
            var repository = new ResumeRepository(context);
            return await repository.CreateVersionAsync(
                Resume("en", $"resumes/{index}.pdf", published: false, active: false),
                2026,
                TestContext.Current.CancellationToken);
        }).ToArray();
        start.SetResult();

        var created = await Task.WhenAll(tasks);

        Assert.Equal(Enumerable.Range(1, count), created.Select(item => item.VersionSequence).Order());
        await using var verification = database.CreateDbContext();
        Assert.Equal(count, await verification.Resumes.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(count, (await verification.ResumeVersionCounters.SingleAsync(TestContext.Current.CancellationToken)).LastSequence);
    }

    [Fact]
    public async Task EnglishAndVietnameseCountersAreIndependent()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var repository = new ResumeRepository(context);

        var english = await repository.CreateVersionAsync(
            Resume("en", "resumes/en.pdf", false, false), 2026, TestContext.Current.CancellationToken);
        var vietnamese = await repository.CreateVersionAsync(
            Resume("vi", "resumes/vi.pdf", false, false), 2026, TestContext.Current.CancellationToken);

        Assert.Equal(1, english.VersionSequence);
        Assert.Equal(1, vietnamese.VersionSequence);
    }

    [Fact]
    public async Task CreateFailureRollsBackCounterMetadataAndActiveSwitch()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using (var setup = database.CreateDbContext())
        {
            setup.ResumeVersionCounters.Add(new ResumeVersionCounter
            {
                LanguageCode = "en",
                VersionYear = 2025,
                LastSequence = 1,
            });
            setup.Resumes.Add(WithVersion(Resume("en", "resumes/old.pdf", true, true), 2025, 1));
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using (var context = database.CreateDbContext())
        {
            var invalid = Resume("en", null!, true, true);
            var repository = new ResumeRepository(context);
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => repository.CreateVersionAsync(
                invalid, 2026, TestContext.Current.CancellationToken));
        }

        await using var verification = database.CreateDbContext();
        Assert.False(await verification.ResumeVersionCounters.AnyAsync(
            item => item.LanguageCode == "en" && item.VersionYear == 2026,
            TestContext.Current.CancellationToken));
        var stored = await verification.Resumes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.True(stored.IsActive);
        Assert.Equal("resumes/old.pdf", stored.FileUrl);
    }

    [Fact]
    public async Task SetCurrentPreservesOneActiveResumePerLanguage()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        Guid replacementId;
        await using (var setup = database.CreateDbContext())
        {
            setup.ResumeVersionCounters.Add(new ResumeVersionCounter
            {
                LanguageCode = "en",
                VersionYear = 2026,
                LastSequence = 2,
            });
            var current = WithVersion(Resume("en", "resumes/one.pdf", true, true), 2026, 1);
            var replacement = WithVersion(Resume("en", "resumes/two.pdf", true, false), 2026, 2);
            replacementId = replacement.Id;
            setup.Resumes.AddRange(current, replacement);
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using (var context = database.CreateDbContext())
        {
            var result = await new ResumeRepository(context).SetCurrentAsync(
                replacementId, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
            Assert.Equal(ResumeActivationStatus.Activated, result.Status);
        }

        await using var verification = database.CreateDbContext();
        var active = await verification.Resumes.Where(item => item.IsActive)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(replacementId, active.Id);
    }

    [Fact]
    public async Task PublicCurrentRequiresMatchingPortfolioPublishedActiveAndLanguage()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var profile = new Profile { Slug = "nam", FullName = "Nam" };
        profile.Translations.Add(new ProfileTranslation { LocaleCode = "en", Title = "Engineer" });
        profile.Translations.Add(new ProfileTranslation { LocaleCode = "vi", Title = "Ky su" });
        context.Profiles.Add(profile);
        context.ResumeVersionCounters.AddRange(
            new ResumeVersionCounter { LanguageCode = "en", VersionYear = 2026, LastSequence = 1 },
            new ResumeVersionCounter { LanguageCode = "vi", VersionYear = 2026, LastSequence = 1 });
        var english = WithVersion(Resume("en", "resumes/en.pdf", true, true), 2026, 1);
        var vietnameseDraft = WithVersion(Resume("vi", "resumes/vi.pdf", false, true), 2026, 1);
        context.Resumes.AddRange(english, vietnameseDraft);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new ResumeRepository(context);

        var result = await repository.GetPublicCurrentAsync(
            "nam", "en", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("English", result.Description);
        Assert.Equal("resumes/en.pdf", result.ObjectKey);
        Assert.Null(await repository.GetPublicCurrentAsync(
            "nam", "vi", TestContext.Current.CancellationToken));
        Assert.Null(await repository.GetPublicCurrentAsync(
            "missing", "en", TestContext.Current.CancellationToken));
    }

    private static ResumeFile Resume(string language, string fileUrl, bool published, bool active)
    {
        var item = new ResumeFile
        {
            Id = Guid.NewGuid(),
            LanguageCode = language,
            FileUrl = fileUrl,
            OriginalFileName = "resume.pdf",
            ContentType = "application/pdf",
            FileSize = 42,
            IsPublished = published,
            IsActive = active,
        };
        item.Translations.Add(new ResumeTranslation { ResumeId = item.Id, LocaleCode = "en", Description = "English" });
        item.Translations.Add(new ResumeTranslation { ResumeId = item.Id, LocaleCode = "vi", Description = "Vietnamese" });
        return item;
    }

    private static ResumeFile WithVersion(ResumeFile item, short year, int sequence)
    {
        item.VersionYear = year;
        item.VersionSequence = sequence;
        return item;
    }
}
