using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Application.Common.Storage;
using Xunit;

namespace Portfolio.UnitTests.Common.Storage;

public sealed class StorageReplacementTests
{
    [Fact]
    public async Task DeletesNewObjectWhenPersistenceFails()
    {
        var storage = new RecordingStorage();
        var replacement = new StorageReplacement(
            storage,
            NullLogger<StorageReplacement>.Instance);
        var upload = CreateUpload();

        await Assert.ThrowsAsync<InvalidOperationException>(() => replacement.ReplaceAsync(
            upload,
            "profiles/old.png",
            (_, _) =>
            {
                storage.Events.Add("persist");
                throw new InvalidOperationException("database failed");
            },
            TestContext.Current.CancellationToken));

        Assert.Equal(["upload", "persist", "delete:profiles/new.png"], storage.Events);
    }

    [Fact]
    public async Task KeepsOldObjectUntilNewMetadataCommits()
    {
        var storage = new RecordingStorage();
        var replacement = new StorageReplacement(
            storage,
            NullLogger<StorageReplacement>.Instance);

        await replacement.ReplaceAsync(
            CreateUpload(),
            "profiles/old.png",
            (_, _) =>
            {
                storage.Events.Add("persist");
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(["upload", "persist", "delete:profiles/old.png"], storage.Events);
    }

    [Fact]
    public async Task CompensationFailureDoesNotHidePersistenceFailure()
    {
        var storage = new RecordingStorage { FailDelete = true };
        var replacement = new StorageReplacement(
            storage,
            NullLogger<StorageReplacement>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            replacement.ReplaceAsync(
                CreateUpload(),
                null,
                (_, _) => throw new InvalidOperationException("database failed"),
                TestContext.Current.CancellationToken));

        Assert.Equal("database failed", exception.Message);
    }

    private static StorageUpload CreateUpload() => new(
        "avatars",
        "profiles/new.png",
        new MemoryStream([1, 2, 3]),
        "image/png",
        3);

    private sealed class RecordingStorage : IFileStorage
    {
        public List<string> Events { get; } = [];
        public bool FailDelete { get; init; }

        public Task<StorageObject> UploadAsync(
            StorageUpload upload,
            CancellationToken cancellationToken)
        {
            Events.Add("upload");
            return Task.FromResult(new StorageObject(upload.Bucket, upload.ObjectKey));
        }

        public Task DeleteIfExistsAsync(
            string bucket,
            string objectKey,
            CancellationToken cancellationToken)
        {
            Events.Add($"delete:{objectKey}");
            return FailDelete
                ? Task.FromException(new IOException("delete failed"))
                : Task.CompletedTask;
        }

        public Task<Uri> CreateSignedReadUrlAsync(
            string bucket,
            string objectKey,
            TimeSpan lifetime,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
