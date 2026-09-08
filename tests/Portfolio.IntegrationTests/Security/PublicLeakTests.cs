using Portfolio.Application.Common.Models;
using Portfolio.Infrastructure.Persistence.Repositories;
using Portfolio.IntegrationTests.Infrastructure;
using Xunit;

namespace Portfolio.IntegrationTests.Security;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class PublicLeakTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task MissingRequestedTranslationsNeverFallBackAcrossPublicFeatures()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var now = new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            Slug = "translation-security",
            FullName = "Fictional Owner",
            CreatedAt = now,
            UpdatedAt = now,
        };
        profile.Translations.Add(new ProfileTranslation
        {
            ProfileId = profile.Id,
            LocaleCode = "en",
            Title = "English title",
            UpdatedAt = now,
        });

        var about = new About
        {
            Id = Guid.NewGuid(),
            ProfileId = profile.Id,
            Profile = profile,
            IsPublished = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        about.Translations.Add(new AboutTranslation
        {
            AboutId = about.Id,
            LocaleCode = "en",
            Content = "English about",
        });

        var category = new SkillCategory
        {
            Id = Guid.NewGuid(),
            IsPublished = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        category.Translations.Add(new SkillCategoryTranslation
        {
            CategoryId = category.Id,
            LocaleCode = "en",
            Name = "English category",
        });

        var experience = new WorkExperience
        {
            Id = Guid.NewGuid(),
            CompanyName = "Fictional Company",
            StartDate = new DateOnly(2026, 1, 1),
            IsPublished = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        experience.Translations.Add(new ExperienceTranslation
        {
            ExperienceId = experience.Id,
            LocaleCode = "en",
            Position = "English position",
        });

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Slug = "english-only-project",
            InternalName = "English only project",
            Kind = ProjectKind.Personal,
            DisclosureLevel = ProjectDisclosureLevel.Full,
            IsPublished = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        project.Translations.Add(new ProjectTranslation
        {
            ProjectId = project.Id,
            LocaleCode = "en",
            Name = "English project",
        });

        var certificate = new Certificate
        {
            Id = Guid.NewGuid(),
            Issuer = "Fictional Issuer",
            IssuedDate = new DateOnly(2026, 1, 1),
            IsPublished = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        certificate.Translations.Add(new CertificateTranslation
        {
            CertificateId = certificate.Id,
            LocaleCode = "en",
            Name = "English certificate",
        });

        var counter = new ResumeVersionCounter
        {
            LanguageCode = "en",
            VersionYear = 2026,
            LastSequence = 1,
            UpdatedAt = now,
        };
        var resume = new ResumeFile
        {
            Id = Guid.NewGuid(),
            LanguageCode = "en",
            FileUrl = "resumes/private/english.pdf",
            OriginalFileName = "english.pdf",
            ContentType = "application/pdf",
            FileSize = 42,
            VersionYear = 2026,
            VersionSequence = 1,
            IsPublished = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            VersionCounter = counter,
        };
        resume.Translations.Add(new ResumeTranslation
        {
            ResumeId = resume.Id,
            LocaleCode = "en",
            Description = "English resume",
        });

        context.AddRange(profile, about, category, experience, project, certificate, counter, resume);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Null(await new ProfileRepository(context).GetPublicAsync(
            profile.Slug, "vi", TestContext.Current.CancellationToken));
        Assert.Null(await new AboutRepository(context).GetPublicAsync(
            profile.Slug, "vi", TestContext.Current.CancellationToken));
        Assert.Empty((await new SkillRepository(context).GetPublicAsync(
            profile.Slug, "vi", TestContext.Current.CancellationToken))!);
        Assert.Empty((await new ExperienceRepository(context).GetPublicAsync(
            profile.Slug, "vi", TestContext.Current.CancellationToken))!);
        Assert.Empty((await new ProjectRepository(context).GetPublicListAsync(
            profile.Slug, "vi", TestContext.Current.CancellationToken))!);
        Assert.Null(await new ProjectRepository(context).GetPublicDetailAsync(
            profile.Slug, project.Slug, "vi", TestContext.Current.CancellationToken));
        Assert.Empty((await new CertificateRepository(context).GetPublicAsync(
            profile.Slug, "vi", TestContext.Current.CancellationToken))!);
        Assert.Null(await new ResumeRepository(context).GetPublicCurrentAsync(
            profile.Slug, "vi", TestContext.Current.CancellationToken));
    }
}
