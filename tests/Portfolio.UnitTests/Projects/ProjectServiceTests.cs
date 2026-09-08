using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Common.Storage;
using Portfolio.Application.Projects;
using Xunit;

namespace Portfolio.UnitTests.Projects;

public sealed class ProjectServiceTests
{
    [Fact]
    public async Task GetLimitedDetailOmitsSensitiveFields()
    {
        var repository = new ProjectRepositoryStub
        {
            PublicDetail = Projection(ProjectDisclosureLevel.Limited),
        };
        var service = CreateService(repository);

        var result = await service.GetPublicDetailAsync(
            "nam", "secret-client", "en", TestContext.Current.CancellationToken);

        var json = JsonSerializer.Serialize(result);
        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("repositoryUrl", names);
        Assert.DoesNotContain("demoUrl", names);
        Assert.DoesNotContain("clientContext", names);
        Assert.DoesNotContain("problem", names);
        Assert.DoesNotContain("solution", names);
        Assert.DoesNotContain("result", names);
        Assert.DoesNotContain("images", names);
    }

    [Fact]
    public async Task CreateRejectsDuplicateNormalizedSlug()
    {
        var repository = new ProjectRepositoryStub { SlugExists = true };
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateProjectAsync(
            Request("Secret Client"), TestContext.Current.CancellationToken));

        Assert.Equal("secret-client", repository.CheckedSlug);
    }

    [Fact]
    public async Task PublishRequiresEveryImageAltText()
    {
        var project = Entity();
        project.Images.Add(new ProjectImage
        {
            Id = Guid.NewGuid(),
            ImageUrl = "projects/p/image.png",
            Translations = { new ProjectImageTranslation { LocaleCode = "en", AltText = "Screenshot" } },
        });
        var service = CreateService(new ProjectRepositoryStub { Existing = project });

        await Assert.ThrowsAsync<ValidationException>(() => service.SetPublishedAsync(
            project.Id, new ProjectPublishRequest(true), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UploadGalleryCompensatesEveryNewObjectWhenSaveFails()
    {
        var project = Entity();
        var repository = new ProjectRepositoryStub { Existing = project, FailSave = true };
        var storage = new RecordingStorage();
        var service = CreateService(repository, storage);
        var uploads = new[] { Upload(0), Upload(1) };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadGalleryAsync(
            project.Id,
            uploads,
            [Metadata(0, 0), Metadata(1, 1)],
            TestContext.Current.CancellationToken));

        Assert.Equal(2, storage.Events.Count(value => value == "upload"));
        Assert.Equal(2, storage.Events.Count(value => value.StartsWith("delete:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task UploadGalleryCompensatesEarlierObjectsWhenLaterUploadFails()
    {
        var project = Entity();
        var storage = new RecordingStorage { FailUploadNumber = 2 };
        var service = CreateService(new ProjectRepositoryStub { Existing = project }, storage);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadGalleryAsync(
            project.Id,
            [Upload(0), Upload(1)],
            [Metadata(0, 0), Metadata(1, 1)],
            TestContext.Current.CancellationToken));

        Assert.Single(storage.Events, value => value.StartsWith("delete:", StringComparison.Ordinal));
        Assert.Empty(project.Images);
    }

    [Fact]
    public async Task ReorderRejectsDuplicateIds()
    {
        var id = Guid.NewGuid();
        var service = CreateService(new ProjectRepositoryStub());

        await Assert.ThrowsAsync<ValidationException>(() => service.ReorderProjectsAsync(
            new ProjectReorderRequest([new(id, 0), new(id, 1)]),
            TestContext.Current.CancellationToken));
    }

    private static ProjectService CreateService(
        ProjectRepositoryStub repository,
        RecordingStorage? storage = null)
    {
        storage ??= new RecordingStorage();
        return new ProjectService(
            repository,
            storage,
            new ProjectImageSettings("project-images", 1024, 10),
            TimeProvider.System,
            NullLogger<ProjectService>.Instance);
    }

    private static ProjectWriteRequest Request(string slug) => new(
        slug,
        "Secret Client",
        ProjectKind.Professional,
        ProjectDisclosureLevel.Limited,
        "https://github.com/example/repository",
        "https://example.com/demo",
        null,
        new DateOnly(2024, 1, 1),
        null,
        true,
        0,
        false,
        new Dictionary<string, ProjectTranslationRequest>
        {
            ["en"] = new("Project", "Description", "Client", "Engineer", "Problem", "Solution", "Result"),
            ["vi"] = new("Du an", "Mo ta", "Khach hang", "Ky su", "Van de", "Giai phap", "Ket qua"),
        },
        [],
        [],
        []);

    private static Project Entity()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Slug = "secret-client",
            InternalName = "Secret Client",
            Kind = ProjectKind.Professional,
            DisclosureLevel = ProjectDisclosureLevel.Full,
        };
        project.Translations.Add(new ProjectTranslation { LocaleCode = "en", Name = "Project" });
        project.Translations.Add(new ProjectTranslation { LocaleCode = "vi", Name = "Du an" });
        return project;
    }

    private static ProjectPublicProjection Projection(ProjectDisclosureLevel disclosure) => new(
        Guid.NewGuid(),
        "secret-client",
        "Project",
        "Description",
        ProjectKind.Professional,
        disclosure,
        "projects/p/thumbnail.png",
        "https://github.com/example/repository",
        "https://example.com/demo",
        new DateOnly(2024, 1, 1),
        null,
        true,
        "Client",
        "Engineer",
        "Problem",
        "Solution",
        "Result",
        [],
        [],
        [new(Guid.NewGuid(), "projects/p/image.png", "Screenshot", 0)]);

    private static ProjectGalleryUpload Upload(int index)
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        return new ProjectGalleryUpload(index, new MemoryStream(bytes), $"image-{index}.png", "image/png", bytes.Length);
    }

    private static ProjectGalleryMetadata Metadata(int fileIndex, int order) => new(
        fileIndex,
        order,
        new Dictionary<string, string> { ["en"] = "Screenshot", ["vi"] = "Anh" });

    private sealed class ProjectRepositoryStub : IProjectRepository
    {
        public ProjectPublicProjection? PublicDetail { get; init; }
        public Project? Existing { get; init; }
        public bool SlugExists { get; init; }
        public bool FailSave { get; init; }
        public string? CheckedSlug { get; private set; }

        public Task<IReadOnlyList<ProjectPublicProjection>?> GetPublicListAsync(string slug, string locale, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<ProjectPublicProjection>?>([]);
        public Task<ProjectPublicProjection?> GetPublicDetailAsync(string slug, string projectSlug, string locale, CancellationToken token) =>
            Task.FromResult(PublicDetail);
        public Task<ProjectEntityPage> GetProjectsAsync(ProjectAdminQuery query, CancellationToken token) =>
            Task.FromResult(new ProjectEntityPage([], 0, query.Page, query.PageSize));
        public Task<Project?> GetAsync(Guid id, bool tracked, CancellationToken token) =>
            Task.FromResult(Existing?.Id == id ? Existing : null);
        public Task<bool> SlugExistsAsync(string slug, Guid? excludedId, CancellationToken token)
        { CheckedSlug = slug; return Task.FromResult(SlugExists); }
        public Task<bool> TechnologyIdsExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => Task.FromResult(true);
        public Task AddAsync(Project project, CancellationToken token) => Task.CompletedTask;
        public void Remove(Project project) { }
        public Task<ProjectReorderResult> ReorderAsync(IReadOnlyList<ProjectOrderItem> items, CancellationToken token) =>
            Task.FromResult(ProjectReorderResult.Success);
        public Task SaveChangesAsync(CancellationToken token) => FailSave
            ? Task.FromException(new InvalidOperationException("database failed"))
            : Task.CompletedTask;
    }

    private sealed class RecordingStorage : IFileStorage
    {
        private int _uploads;
        public int? FailUploadNumber { get; init; }
        public List<string> Events { get; } = [];

        public Task<StorageObject> UploadAsync(StorageUpload upload, CancellationToken token)
        {
            _uploads++;
            Events.Add("upload");
            if (_uploads == FailUploadNumber) throw new InvalidOperationException("upload failed");
            return Task.FromResult(new StorageObject(upload.Bucket, upload.ObjectKey));
        }

        public Task DeleteIfExistsAsync(string bucket, string objectKey, CancellationToken token)
        { Events.Add($"delete:{objectKey}"); return Task.CompletedTask; }
        public Uri GetPublicReadUrl(string bucket, string objectKey) => new($"https://storage.example/{bucket}/{objectKey}");
        public Task<Uri> CreateSignedReadUrlAsync(string bucket, string objectKey, TimeSpan lifetime, CancellationToken token) =>
            throw new NotSupportedException();
    }
}
