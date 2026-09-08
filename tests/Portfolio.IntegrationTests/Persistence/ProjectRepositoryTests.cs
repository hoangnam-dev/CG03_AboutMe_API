using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Common.Storage;
using Portfolio.Application.Projects;
using Portfolio.Infrastructure.Persistence.Repositories;
using Portfolio.IntegrationTests.Infrastructure;
using Xunit;

namespace Portfolio.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class ProjectRepositoryTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task PublicListFiltersDraftsAndOrdersFeaturedThenDisplayOrderThenId()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        context.Profiles.Add(Profile());
        context.Projects.AddRange(
            Project("ordinary", false, 0, true),
            Project("featured-two", true, 2, true),
            Project("featured-one", true, 1, true),
            Project("draft", true, 0, false));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new ProjectRepository(context);

        var result = await repository.GetPublicListAsync(
            "nam", "en", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(
            ["featured-one", "featured-two", "ordinary"],
            result.Select(item => item.Slug));
    }

    [Fact]
    public async Task LimitedDetailDoesNotProjectSensitiveDataOrGallery()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        context.Profiles.Add(Profile());
        var project = Project("client", true, 0, true, ProjectDisclosureLevel.Limited);
        project.RepositoryUrl = "https://github.com/example/private";
        project.DemoUrl = "https://example.com/private";
        project.Translations.Single(item => item.LocaleCode == "en").ClientContext = "Secret";
        project.Images.Add(Image(project.Id));
        context.Projects.Add(project);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new ProjectRepository(context);

        var result = await repository.GetPublicDetailAsync(
            "nam", "client", "en", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Null(result.RepositoryUrl);
        Assert.Null(result.DemoUrl);
        Assert.Null(result.ClientContext);
        Assert.Empty(result.Images);
    }

    [Fact]
    public async Task AggregateSavePersistsTranslationsHighlightsImagesAndTechnologies()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var category = Category();
        var technology = Technology(category);
        context.Technologies.Add(technology);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var project = Project("aggregate", false, 0, false);
        project.Highlights.Add(new ProjectHighlight
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            LocaleCode = "en",
            Content = "Fast",
            DisplayOrder = 0,
        });
        project.Technologies.Add(new ProjectTechnology
        {
            ProjectId = project.Id,
            TechnologyId = technology.Id,
            DisplayOrder = 0,
        });
        project.Images.Add(Image(project.Id));
        var repository = new ProjectRepository(context);

        await repository.AddAsync(project, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var stored = await repository.GetAsync(
            project.Id, false, TestContext.Current.CancellationToken);

        Assert.NotNull(stored);
        Assert.Equal(2, stored.Translations.Count);
        Assert.Single(stored.Highlights);
        Assert.Single(stored.Technologies);
        Assert.Equal(2, Assert.Single(stored.Images).Translations.Count);
    }

    [Fact]
    public async Task ReorderRejectsPartialSetWithoutChangingOrders()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var first = Project("first", false, 0, false);
        var second = Project("second", false, 1, false);
        context.Projects.AddRange(first, second);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new ProjectRepository(context);

        var result = await repository.ReorderAsync(
            [new ProjectOrderItem(first.Id, 9)], TestContext.Current.CancellationToken);

        Assert.Equal(ProjectReorderResult.SetMismatch, result);
        context.ChangeTracker.Clear();
        Assert.Equal([0, 1], context.Projects.OrderBy(item => item.DisplayOrder).Select(item => item.DisplayOrder));
    }

    [Fact]
    public async Task UpdateChangesExistingImageAltTextWithoutTrackingConflict()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var project = Project("update-image", false, 0, false);
        var image = Image(project.Id);
        project.Images.Add(image);
        context.Projects.Add(project);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var repository = new ProjectRepository(context);
        var service = new ProjectService(
            repository,
            new PublicUrlStorage(),
            new ProjectImageSettings("project-images", 1024, 10),
            TimeProvider.System,
            NullLogger<ProjectService>.Instance);
        var request = new ProjectWriteRequest(
            project.Slug, project.InternalName, project.Kind, project.DisclosureLevel,
            null, null, null, null, null, false, 0, false,
            new Dictionary<string, ProjectTranslationRequest>
            {
                ["en"] = new("update-image", null, null, null, null, null, null),
                ["vi"] = new("vi-update-image", null, null, null, null, null, null),
            },
            [],
            [],
            [new ProjectImageMetadataRequest(
                image.Id,
                0,
                new Dictionary<string, string> { ["en"] = "Updated", ["vi"] = "Da sua" })]);

        await service.UpdateProjectAsync(project.Id, request, TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();
        var stored = await repository.GetAsync(project.Id, false, TestContext.Current.CancellationToken);
        Assert.Equal("Updated", stored!.Images.Single().Translations.Single(item => item.LocaleCode == "en").AltText);
    }

    private static Profile Profile()
    {
        var profile = new Profile { Slug = "nam", FullName = "Nam" };
        profile.Translations.Add(new ProfileTranslation { LocaleCode = "en", Title = "Engineer" });
        profile.Translations.Add(new ProfileTranslation { LocaleCode = "vi", Title = "Ky su" });
        return profile;
    }

    private static Project Project(
        string slug,
        bool featured,
        int order,
        bool published,
        ProjectDisclosureLevel disclosure = ProjectDisclosureLevel.Full)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            InternalName = slug,
            Kind = ProjectKind.Personal,
            DisclosureLevel = disclosure,
            IsFeatured = featured,
            DisplayOrder = order,
            IsPublished = published,
        };
        project.Translations.Add(new ProjectTranslation
        {
            ProjectId = project.Id,
            LocaleCode = "en",
            Name = slug,
            Role = "Engineer",
            Problem = "Problem",
            Solution = "Solution",
            Result = "Result",
        });
        project.Translations.Add(new ProjectTranslation
        {
            ProjectId = project.Id,
            LocaleCode = "vi",
            Name = $"vi-{slug}",
            Role = "Ky su",
        });
        return project;
    }

    private static ProjectImage Image(Guid projectId)
    {
        var image = new ProjectImage
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ImageUrl = $"projects/{projectId}/image.png",
            DisplayOrder = 0,
        };
        image.Translations.Add(new ProjectImageTranslation
        {
            ProjectImageId = image.Id,
            LocaleCode = "en",
            AltText = "Screenshot",
        });
        image.Translations.Add(new ProjectImageTranslation
        {
            ProjectImageId = image.Id,
            LocaleCode = "vi",
            AltText = "Anh",
        });
        return image;
    }

    private static SkillCategory Category()
    {
        var category = new SkillCategory { DisplayOrder = 0, IsPublished = true };
        category.Translations.Add(new SkillCategoryTranslation { LocaleCode = "en", Name = "Backend" });
        category.Translations.Add(new SkillCategoryTranslation { LocaleCode = "vi", Name = "May chu" });
        return category;
    }

    private static Technology Technology(SkillCategory category)
    {
        var technology = new Technology
        {
            Id = Guid.NewGuid(),
            Category = category,
            SkillLevel = SkillLevel.Primary,
            IconType = SkillIconType.Lucide,
            IconValue = "database",
            IsPublished = true,
        };
        technology.Translations.Add(new TechnologyTranslation { LocaleCode = "en", Name = "PostgreSQL" });
        technology.Translations.Add(new TechnologyTranslation { LocaleCode = "vi", Name = "PostgreSQL VI" });
        return technology;
    }

    private sealed class PublicUrlStorage : IFileStorage
    {
        public Task<StorageObject> UploadAsync(StorageUpload upload, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteIfExistsAsync(string bucket, string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;
        public Uri GetPublicReadUrl(string bucket, string objectKey) => new($"https://storage.example/{bucket}/{objectKey}");
        public Task<Uri> CreateSignedReadUrlAsync(string bucket, string objectKey, TimeSpan lifetime, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
