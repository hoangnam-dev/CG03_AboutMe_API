namespace Portfolio.Application.Common.Storage;

public sealed record StorageUpload(
    string Bucket,
    string ObjectKey,
    Stream Content,
    string ContentType,
    long Length);
