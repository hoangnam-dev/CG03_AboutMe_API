using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Common.Storage;
using Portfolio.Application.Resumes;
using Xunit;

namespace Portfolio.UnitTests.Resumes;

public sealed class ResumeServiceTests
{
    private static readonly DateTimeOffset YearBoundary =
        new(2026, 12, 31, 18, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(7, "v2027_07")]
    [InlineData(123, "v2027_123")]
    public void VersionFormatterUsesAtLeastTwoSequenceDigits(int sequence, string expected) =>
        Assert.Equal(expected, ResumeVersionFormatter.Format(2027, sequence));

    [Theory]
    [InlineData("", "application/pdf")]
    [InlineData("resume.txt", "text/plain")]
    public async Task UploadRejectsZeroByteOrNonPdf(string fileName, string contentType)
    {
        var service = CreateService(new RepositoryStub(), new StorageStub());
        await using var content = fileName.Length == 0
            ? new MemoryStream()
            : new MemoryStream("not pdf"u8.ToArray());

        await Assert.ThrowsAsync<ValidationException>(() => service.UploadAsync(
            Upload(content, fileName, contentType), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UploadRequiresPublishedBeforeActive()
    {
        var storage = new StorageStub();
        var service = CreateService(new RepositoryStub(), storage);
        await using var content = Pdf();

        await Assert.ThrowsAsync<ValidationException>(() => service.UploadAsync(
            Upload(content, isPublished: false, isActive: true),
            TestContext.Current.CancellationToken));

        Assert.Empty(storage.UploadedKeys);
    }

    [Fact]
    public async Task UploadAllocatesVersionUsingHoChiMinhYearAndGeneratedObjectKey()
    {
        var repository = new RepositoryStub();
        var storage = new StorageStub();
        var service = CreateService(repository, storage);
        await using var content = Pdf();

        var result = await service.UploadAsync(
            Upload(content, isPublished: true, isActive: true),
            TestContext.Current.CancellationToken);

        Assert.Equal((short)2027, repository.AllocatedYear);
        Assert.Equal("v2027_01", result.Version);
        Assert.True(result.IsActive);
        Assert.StartsWith($"resumes/{result.Id}/", Assert.Single(storage.UploadedKeys), StringComparison.Ordinal);
        Assert.EndsWith(".pdf", storage.UploadedKeys[0], StringComparison.Ordinal);
        Assert.DoesNotContain("resume.pdf", storage.UploadedKeys[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadPersistenceFailureCompensatesNewObject()
    {
        var repository = new RepositoryStub { CreateException = new InvalidOperationException("db") };
        var storage = new StorageStub();
        var service = CreateService(repository, storage);
        await using var content = Pdf();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(
            Upload(content), TestContext.Current.CancellationToken));

        Assert.Equal(storage.UploadedKeys, storage.DeletedKeys);
    }

    [Fact]
    public async Task PublicCurrentCreatesFiveMinuteSignedUrl()
    {
        var repository = new RepositoryStub
        {
            Public = new ResumePublicProjection(
                Guid.NewGuid(), "en", "resumes/id/cv.pdf", "resume.pdf",
                "application/pdf", 42, 2026, 7, "Description"),
        };
        var storage = new StorageStub();
        var service = CreateService(repository, storage);

        var result = await service.GetPublicCurrentAsync(
            "nam", "en", TestContext.Current.CancellationToken);

        Assert.Equal("v2026_07", result.Version);
        Assert.Equal("https://storage.example/signed", result.DownloadUrl.AbsoluteUri);
        Assert.Equal(YearBoundary.AddMinutes(5), result.DownloadUrlExpiresAt);
        Assert.Equal(TimeSpan.FromMinutes(5), storage.SignedLifetime);
    }

    [Fact]
    public async Task SetCurrentDoesNotAllocateNewVersion()
    {
        var resume = Resume(isPublished: true, isActive: false);
        var repository = new RepositoryStub
        {
            Activation = new ResumeActivationResult(ResumeActivationStatus.Activated, resume),
        };
        var service = CreateService(repository, new StorageStub());

        var result = await service.SetCurrentAsync(
            resume.Id, new ResumeCurrentRequest(true), TestContext.Current.CancellationToken);

        Assert.Equal("v2026_04", result.Version);
        Assert.Equal(0, repository.CreateCalls);
        Assert.Equal(1, repository.ActivationCalls);
        Assert.Equal(YearBoundary, repository.ActivationUpdatedAt);
    }

    [Fact]
    public async Task SetCurrentRejectsFalseAndUnpublishedTarget()
    {
        var repository = new RepositoryStub
        {
            Activation = new ResumeActivationResult(ResumeActivationStatus.NotPublished, null),
        };
        var service = CreateService(repository, new StorageStub());

        await Assert.ThrowsAsync<ValidationException>(() => service.SetCurrentAsync(
            Guid.NewGuid(), new ResumeCurrentRequest(false), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ValidationException>(() => service.SetCurrentAsync(
            Guid.NewGuid(), new ResumeCurrentRequest(true), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ActiveResumeCannotBeUnpublishedOrDeleted()
    {
        var resume = Resume(isPublished: true, isActive: true);
        var service = CreateService(new RepositoryStub { Existing = resume }, new StorageStub());

        await Assert.ThrowsAsync<ConflictException>(() => service.SetPublishedAsync(
            resume.Id, new ResumePublishRequest(false), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ConflictException>(() => service.DeleteAsync(
            resume.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteCommitsBeforeRemovingPrivateObject()
    {
        var events = new List<string>();
        var resume = Resume(isPublished: false, isActive: false);
        var repository = new RepositoryStub { Existing = resume, Events = events };
        var storage = new StorageStub { Events = events };
        var service = CreateService(repository, storage);

        await service.DeleteAsync(resume.Id, TestContext.Current.CancellationToken);

        Assert.Equal(["save", $"delete:{resume.FileUrl}"], events);
    }

    private static ResumeService CreateService(IResumeRepository repository, IFileStorage storage) =>
        new(
            repository,
            storage,
            new ResumeSettings("cv-files", 10 * 1024 * 1024, TimeSpan.FromMinutes(5)),
            new FixedTimeProvider(YearBoundary),
            NullLogger<ResumeService>.Instance);

    private static ResumeUploadRequest Upload(
        Stream content,
        string fileName = "resume.pdf",
        string contentType = "application/pdf",
        bool isPublished = false,
        bool isActive = false) =>
        new(content, fileName, contentType, content.Length, "en", "English", "Vietnamese", isPublished, isActive);

    private static MemoryStream Pdf() => new("%PDF-test"u8.ToArray());

    private static ResumeFile Resume(bool isPublished, bool isActive)
    {
        var resume = new ResumeFile
        {
            Id = Guid.NewGuid(),
            LanguageCode = "en",
            FileUrl = "resumes/id/resume.pdf",
            OriginalFileName = "resume.pdf",
            ContentType = "application/pdf",
            FileSize = 42,
            VersionYear = 2026,
            VersionSequence = 4,
            IsPublished = isPublished,
            IsActive = isActive,
            CreatedAt = YearBoundary,
            UpdatedAt = YearBoundary,
        };
        resume.Translations.Add(new ResumeTranslation { ResumeId = resume.Id, LocaleCode = "en", Description = "English" });
        resume.Translations.Add(new ResumeTranslation { ResumeId = resume.Id, LocaleCode = "vi", Description = "Vietnamese" });
        return resume;
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class RepositoryStub : IResumeRepository
    {
        public ResumePublicProjection? Public { get; init; }
        public ResumeFile? Existing { get; init; }
        public ResumeActivationResult Activation { get; init; } =
            new(ResumeActivationStatus.NotFound, null);
        public Exception? CreateException { get; init; }
        public short? AllocatedYear { get; private set; }
        public int CreateCalls { get; private set; }
        public int ActivationCalls { get; private set; }
        public DateTimeOffset? ActivationUpdatedAt { get; private set; }
        public List<string>? Events { get; init; }

        public Task<ResumePublicProjection?> GetPublicCurrentAsync(string slug, string language, CancellationToken token) => Task.FromResult(Public);
        public Task<ResumeEntityPage> GetResumesAsync(ResumeAdminQuery query, CancellationToken token) => Task.FromResult(new ResumeEntityPage([], 0, query.Page, query.PageSize));
        public Task<ResumeFile?> GetAsync(Guid id, bool tracked, CancellationToken token) => Task.FromResult(Existing?.Id == id ? Existing : null);

        public Task<ResumeFile> CreateVersionAsync(ResumeFile resume, short versionYear, CancellationToken token)
        {
            CreateCalls++;
            AllocatedYear = versionYear;
            if (CreateException is not null) return Task.FromException<ResumeFile>(CreateException);
            resume.VersionYear = versionYear;
            resume.VersionSequence = 1;
            return Task.FromResult(resume);
        }

        public Task<ResumeActivationResult> SetCurrentAsync(Guid id, DateTimeOffset updatedAt, CancellationToken token)
        {
            ActivationCalls++;
            ActivationUpdatedAt = updatedAt;
            if (Activation.Resume is not null) Activation.Resume.IsActive = true;
            return Task.FromResult(Activation);
        }

        public void Remove(ResumeFile resume) { }

        public Task SaveChangesAsync(CancellationToken token)
        {
            Events?.Add("save");
            return Task.CompletedTask;
        }
    }

    private sealed class StorageStub : IFileStorage
    {
        public List<string> UploadedKeys { get; } = [];
        public List<string> DeletedKeys { get; } = [];
        public List<string>? Events { get; init; }
        public TimeSpan? SignedLifetime { get; private set; }

        public Task<StorageObject> UploadAsync(StorageUpload upload, CancellationToken token)
        {
            UploadedKeys.Add(upload.ObjectKey);
            return Task.FromResult(new StorageObject(upload.Bucket, upload.ObjectKey));
        }

        public Task DeleteIfExistsAsync(string bucket, string objectKey, CancellationToken token)
        {
            DeletedKeys.Add(objectKey);
            Events?.Add($"delete:{objectKey}");
            return Task.CompletedTask;
        }

        public Uri GetPublicReadUrl(string bucket, string objectKey) => throw new NotSupportedException();

        public Task<Uri> CreateSignedReadUrlAsync(string bucket, string objectKey, TimeSpan lifetime, CancellationToken token)
        {
            SignedLifetime = lifetime;
            return Task.FromResult(new Uri("https://storage.example/signed"));
        }
    }
}
