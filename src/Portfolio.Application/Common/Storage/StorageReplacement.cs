using Microsoft.Extensions.Logging;

namespace Portfolio.Application.Common.Storage;

public sealed partial class StorageReplacement(
    IFileStorage storage,
    ILogger<StorageReplacement> logger)
{
    public async Task<StorageObject> ReplaceAsync(
        StorageUpload upload,
        string? oldObjectKey,
        Func<StorageObject, CancellationToken, Task> persist,
        CancellationToken cancellationToken)
    {
        var newObject = await storage.UploadAsync(upload, cancellationToken);

        try
        {
            await persist(newObject, cancellationToken);
        }
        catch (Exception persistenceException)
        {
            try
            {
                await storage.DeleteIfExistsAsync(
                    newObject.Bucket,
                    newObject.ObjectKey,
                    CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                LogCompensationFailure(
                    logger,
                    newObject.Bucket,
                    newObject.ObjectKey,
                    cleanupException);
            }

            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(persistenceException)
                .Throw();
            throw;
        }

        if (!string.IsNullOrWhiteSpace(oldObjectKey) &&
            !string.Equals(oldObjectKey, newObject.ObjectKey, StringComparison.Ordinal))
        {
            await storage.DeleteIfExistsAsync(
                newObject.Bucket,
                oldObjectKey,
                cancellationToken);
        }

        return newObject;
    }

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Error,
        Message = "Storage compensation failed for bucket {Bucket} and object {ObjectKey}; reconciliation is required.")]
    private static partial void LogCompensationFailure(
        ILogger logger,
        string bucket,
        string objectKey,
        Exception exception);
}
