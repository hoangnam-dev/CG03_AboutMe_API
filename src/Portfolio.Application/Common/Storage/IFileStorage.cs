namespace Portfolio.Application.Common.Storage;

public interface IFileStorage
{
    Task<StorageObject> UploadAsync(
        StorageUpload upload,
        CancellationToken cancellationToken);

    Task DeleteIfExistsAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken);

    Task<Uri> CreateSignedReadUrlAsync(
        string bucket,
        string objectKey,
        TimeSpan lifetime,
        CancellationToken cancellationToken);
}
