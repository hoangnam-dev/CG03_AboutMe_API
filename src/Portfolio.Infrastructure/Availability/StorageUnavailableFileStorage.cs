using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Storage;

namespace Portfolio.Infrastructure.Availability;

internal sealed class StorageUnavailableFileStorage : IFileStorage
{
    public Task<StorageObject> UploadAsync(
        StorageUpload upload,
        CancellationToken cancellationToken) => throw Unavailable();

    public Task DeleteIfExistsAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken) => throw Unavailable();

    public Task<Uri> CreateSignedReadUrlAsync(
        string bucket,
        string objectKey,
        TimeSpan lifetime,
        CancellationToken cancellationToken) => throw Unavailable();

    private static ServiceUnavailableException Unavailable() =>
        new("File storage is not configured.");
}
