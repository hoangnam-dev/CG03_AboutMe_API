using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Common.Storage;
using Portfolio.Application.Skills;
using Xunit;

namespace Portfolio.UnitTests.Skills;

public sealed class SkillServiceTests
{
    private static readonly SkillIconSettings Settings = new(
        "skill-icons", 512 * 1024, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    [Fact]
    public async Task CreateSkillRejectsDuplicateNameWithinCategoryAndLocale()
    {
        var categoryId = Guid.NewGuid();
        var repository = new SkillRepositoryStub
        {
            Category = Category(categoryId, published: true),
            DuplicateName = true,
        };
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateSkillAsync(
            SkillRequest(categoryId, published: false), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreatePublishedSkillRequiresPublishedCategory()
    {
        var categoryId = Guid.NewGuid();
        var repository = new SkillRepositoryStub
        {
            Category = Category(categoryId, published: false),
        };
        var service = CreateService(repository);

        var error = await Assert.ThrowsAsync<ValidationException>(() => service.CreateSkillAsync(
            SkillRequest(categoryId, published: true), TestContext.Current.CancellationToken));

        Assert.Contains("published", error.Errors["categoryId"][0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteCategoryWithTechnologiesReturnsConflict()
    {
        var repository = new SkillRepositoryStub
        {
            Category = Category(Guid.NewGuid(), published: false),
            CategoryHasSkills = true,
        };
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ConflictException>(() => service.DeleteCategoryAsync(
            repository.Category.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReorderRejectsDuplicateIds()
    {
        var id = Guid.NewGuid();
        var service = CreateService(new SkillRepositoryStub());

        await Assert.ThrowsAsync<ValidationException>(() => service.ReorderSkillsAsync(
            Guid.NewGuid(), new SkillReorderRequest([new(id, 0), new(id, 1)]),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateCategoryPublishingRequiresEnglishAndVietnameseNames()
    {
        var service = CreateService(new SkillRepositoryStub());
        var request = new SkillCategoryWriteRequest(0, true,
            new Dictionary<string, SkillTranslationRequest> { ["en"] = new("Backend") });

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateCategoryAsync(
            request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReplaceIconDeletesNewObjectWhenPersistenceFails()
    {
        var skillId = Guid.NewGuid();
        var repository = new SkillRepositoryStub
        {
            Skill = new Technology
            {
                Id = skillId,
                IconType = SkillIconType.Text,
                IconValue = "C#",
            },
            SaveException = new InvalidOperationException("database failed"),
        };
        var storage = new RecordingStorage();
        var service = CreateService(repository, storage);
        var bytes = "<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M1 1h2\"/></svg>"u8.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReplaceIconAsync(
            skillId,
            new SkillIconUpload(new MemoryStream(bytes), "icon.svg", "image/svg+xml", bytes.Length),
            TestContext.Current.CancellationToken));

        Assert.Equal("upload", storage.Events[0]);
        Assert.StartsWith("delete:skills/", storage.Events[1], StringComparison.Ordinal);
    }

    private static SkillService CreateService(
        ISkillRepository repository,
        IFileStorage? storage = null)
    {
        storage ??= new RecordingStorage();
        return new SkillService(
            repository,
            Settings,
            TimeProvider.System,
            NullLogger<SkillService>.Instance,
            storage,
            new StorageReplacement(storage, NullLogger<StorageReplacement>.Instance));
    }

    private static SkillCategory Category(Guid id, bool published) => new()
    {
        Id = id,
        IsPublished = published,
    };

    private static SkillWriteRequest SkillRequest(Guid categoryId, bool published) => new(
        categoryId,
        "Primary",
        3.5m,
        new SkillIconRequest("lucide", "database-zap"),
        0,
        published,
        new Dictionary<string, SkillTranslationRequest>
        {
            ["en"] = new("PostgreSQL"),
            ["vi"] = new("PostgreSQL"),
        });

    private sealed class SkillRepositoryStub : ISkillRepository
    {
        public SkillCategory? Category { get; init; }
        public bool DuplicateName { get; init; }
        public bool CategoryHasSkills { get; init; }
        public Technology? Skill { get; init; }
        public Exception? SaveException { get; init; }

        public Task<IReadOnlyList<PublicSkillCategoryResponse>?> GetPublicAsync(string slug, string locale, CancellationToken token) => throw new NotImplementedException();
        public Task<IReadOnlyList<SkillCategoryAdminResponse>> GetCategoriesAsync(CancellationToken token) => throw new NotImplementedException();
        public Task<SkillCategory?> GetCategoryForUpdateAsync(Guid id, CancellationToken token) => Task.FromResult(Category?.Id == id ? Category : null);
        public Task<bool> CategoryNameExistsAsync(Guid categoryId, string locale, string name, Guid? excludedId, CancellationToken token) => Task.FromResult(false);
        public Task AddCategoryAsync(SkillCategory category, CancellationToken token) => Task.CompletedTask;
        public Task<bool> CategoryHasSkillsAsync(Guid id, CancellationToken token) => Task.FromResult(CategoryHasSkills);
        public void RemoveCategory(SkillCategory category) { }
        public Task<SkillAdminPage> GetSkillsAsync(SkillAdminQuery query, CancellationToken token) => throw new NotImplementedException();
        public Task<Technology?> GetSkillAsync(Guid id, bool tracked, CancellationToken token) => Task.FromResult(Skill?.Id == id ? Skill : null);
        public Task<bool> SkillNameExistsAsync(Guid categoryId, string locale, string name, Guid? excludedId, CancellationToken token) => Task.FromResult(DuplicateName);
        public Task AddSkillAsync(Technology skill, CancellationToken token) => Task.CompletedTask;
        public Task<bool> SkillHasReferencesAsync(Guid id, CancellationToken token) => Task.FromResult(false);
        public void RemoveSkill(Technology skill) { }
        public Task<SkillReorderResult> ReorderSkillsAsync(Guid categoryId, IReadOnlyList<SkillOrderItem> items, CancellationToken token) => Task.FromResult(SkillReorderResult.Success);
        public Task SaveChangesAsync(CancellationToken token) => SaveException is null
            ? Task.CompletedTask
            : Task.FromException(SaveException);
    }

    private sealed class RecordingStorage : IFileStorage
    {
        public List<string> Events { get; } = [];
        public Task<StorageObject> UploadAsync(StorageUpload upload, CancellationToken token)
        {
            Events.Add("upload");
            return Task.FromResult(new StorageObject(upload.Bucket, upload.ObjectKey));
        }
        public Task DeleteIfExistsAsync(string bucket, string objectKey, CancellationToken token)
        {
            Events.Add($"delete:{objectKey}");
            return Task.CompletedTask;
        }
        public Uri GetPublicReadUrl(string bucket, string objectKey) =>
            new($"https://storage.example/storage/v1/object/public/{bucket}/{objectKey}");
        public Task<Uri> CreateSignedReadUrlAsync(string bucket, string objectKey, TimeSpan lifetime, CancellationToken token) =>
            throw new NotSupportedException();
    }
}
