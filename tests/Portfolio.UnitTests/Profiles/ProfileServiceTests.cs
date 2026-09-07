using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Storage;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Profiles;
using System.Text.Json;
using Xunit;

namespace Portfolio.UnitTests.Profiles;

public sealed class ProfileServiceTests
{
    [Fact]
    public async Task GetPublicHidesPrivateContactFieldsAndReturnsRequestedTranslationOnly()
    {
        var repository = new ProfileRepositoryStub
        {
            Public = new ProfilePublicProjection(
                "nam", "Nam", "Backend Engineer", "Bio", "HCMC", "Available", true,
                "private@example.com", "+84000", false, false, null, null,
                [new("github", "GitHub", "https://github.com/example", "github", 0)]),
        };
        var service = CreateService(repository);

        var result = await service.GetPublicAsync(
            "Nam", "vi", TestContext.Current.CancellationToken);

        Assert.Null(result.Email);
        Assert.Null(result.Phone);
        Assert.Equal("Backend Engineer", result.Title);
        Assert.Equal("vi", repository.LastLocale);
        Assert.Equal("nam", repository.LastSlug);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateRejectsDuplicateNormalizedSlug()
    {
        var repository = new ProfileRepositoryStub { SlugExists = true };
        var service = CreateService(repository);

        var error = await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(
            ValidRequest("Nguyễn Nam"), TestContext.Current.CancellationToken));

        Assert.Contains("slug", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("nguyen-nam", repository.CheckedSlug);
    }

    [Fact]
    public async Task UpdateRejectsIncompletePublishedTranslations()
    {
        var service = CreateService(new ProfileRepositoryStub());
        var request = ValidRequest("nam") with
        {
            Translations = new Dictionary<string, ProfileTranslationRequest>
            {
                ["en"] = new("Engineer", null, null, null),
            },
        };

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(
            request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReplaceAvatarCompensatesOnPersistenceFailure()
    {
        var repository = new ProfileRepositoryStub
        {
            Profile = new Profile
            {
                Id = Guid.NewGuid(), Slug = "nam", FullName = "Nam",
                AvatarUrl = "profiles/old.png",
            },
            FailSave = true,
        };
        var storage = new RecordingStorage();
        var service = CreateService(repository, storage);
        await using var content = new MemoryStream(
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReplaceAvatarAsync(
            new ProfileMediaUpload(content, "avatar.png", "image/png", content.Length),
            TestContext.Current.CancellationToken));

        Assert.Equal("upload", storage.Events[0]);
        Assert.StartsWith("delete:profiles/", storage.Events[1], StringComparison.Ordinal);
        Assert.DoesNotContain("delete:profiles/old.png", storage.Events);
    }

    [Fact]
    public async Task ReplaceHeroImageRejectsConfiguredDimensionLimit()
    {
        var repository = new ProfileRepositoryStub
        {
            Profile = new Profile { Id = Guid.NewGuid(), Slug = "nam", FullName = "Nam" },
        };
        var storage = new RecordingStorage();
        var service = CreateService(repository, storage);
        var png = new byte[24];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(png, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16, 4), 9000);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20, 4), 1000);
        await using var content = new MemoryStream(png);

        await Assert.ThrowsAsync<ValidationException>(() => service.ReplaceHeroImageAsync(
            new ProfileMediaUpload(content, "hero.png", "image/png", content.Length),
            TestContext.Current.CancellationToken));

        Assert.Empty(storage.Events);
    }

    private static ProfileUpdateRequest ValidRequest(string slug) => new(
        slug, "Nguyen Nam", "admin@example.com", "+84", false, false, true,
        new Dictionary<string, ProfileTranslationRequest>
        {
            ["en"] = new("Engineer", null, null, null),
            ["vi"] = new("Ky su", null, null, null),
        },
        [new("github", "GitHub", "https://github.com/example", "github", 0, true)]);

    private static ProfileService CreateService(
        ProfileRepositoryStub repository,
        RecordingStorage? storage = null)
    {
        storage ??= new RecordingStorage();
        return new ProfileService(
            repository,
            storage,
            new StorageReplacement(storage, NullLogger<StorageReplacement>.Instance),
            new ProfileMediaSettings("avatars", 1024),
            TimeProvider.System,
            NullLogger<ProfileService>.Instance);
    }

    private sealed class ProfileRepositoryStub : IProfileRepository
    {
        public ProfilePublicProjection? Public { get; init; }
        public Profile? Profile { get; init; }
        public bool SlugExists { get; init; }
        public bool FailSave { get; init; }
        public string? LastLocale { get; private set; }
        public string? LastSlug { get; private set; }
        public string? CheckedSlug { get; private set; }

        public Task<ProfilePublicProjection?> GetPublicAsync(string slug, string locale, CancellationToken token)
        { LastSlug = slug; LastLocale = locale; return Task.FromResult(Public); }
        public Task<Profile?> GetAdminAsync(CancellationToken token) => Task.FromResult(Profile);
        public Task<Profile?> GetForUpdateAsync(CancellationToken token) => Task.FromResult(Profile);
        public Task<bool> SlugExistsAsync(string slug, Guid? excludedId, CancellationToken token)
        { CheckedSlug = slug; return Task.FromResult(SlugExists); }
        public Task AddAsync(Profile profile, CancellationToken token) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken token) => FailSave
            ? Task.FromException(new InvalidOperationException("database failed"))
            : Task.CompletedTask;
    }

    private sealed class RecordingStorage : IFileStorage
    {
        public List<string> Events { get; } = [];
        public Task<StorageObject> UploadAsync(StorageUpload upload, CancellationToken token)
        { Events.Add("upload"); return Task.FromResult(new StorageObject(upload.Bucket, upload.ObjectKey)); }
        public Task DeleteIfExistsAsync(string bucket, string objectKey, CancellationToken token)
        { Events.Add($"delete:{objectKey}"); return Task.CompletedTask; }
        public Uri GetPublicReadUrl(string bucket, string objectKey) =>
            new($"https://storage.example/{bucket}/{objectKey}");
        public Task<Uri> CreateSignedReadUrlAsync(string bucket, string objectKey, TimeSpan lifetime, CancellationToken token) =>
            throw new NotSupportedException();
    }
}
