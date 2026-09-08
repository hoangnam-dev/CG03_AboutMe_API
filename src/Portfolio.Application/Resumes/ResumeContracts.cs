namespace Portfolio.Application.Resumes;

public sealed record ResumeUploadRequest(
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long Length,
    string Language,
    string? DescriptionEn,
    string? DescriptionVi,
    bool IsPublished,
    bool IsActive);

public sealed record ResumeCurrentRequest(bool IsCurrent);

public sealed record ResumePublishRequest(bool IsPublished);

public sealed record ResumeTranslationResponse(string? Description);

public sealed record ResumePublicProjection(
    Guid Id,
    string Language,
    string ObjectKey,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    short VersionYear,
    int VersionSequence,
    string? Description);

public sealed record ResumePublicResponse(
    string Language,
    string Version,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    string? Description,
    Uri DownloadUrl,
    DateTimeOffset DownloadUrlExpiresAt);

public sealed record ResumeAdminResponse(
    Guid Id,
    string Language,
    string Version,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    IReadOnlyDictionary<string, ResumeTranslationResponse> Translations,
    bool IsPublished,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record ResumeAdminQuery(string? Language, int Page, int PageSize);

public sealed record ResumeEntityPage(
    IReadOnlyList<Portfolio.Application.Common.Models.ResumeFile> Items,
    long Total,
    int Page,
    int PageSize);

public sealed record ResumeAdminPage(
    IReadOnlyList<ResumeAdminResponse> Items,
    long Total,
    int Page,
    int PageSize)
{
    public int TotalPages => Total == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

public enum ResumeActivationStatus
{
    Activated,
    NotFound,
    NotPublished,
}

public sealed record ResumeActivationResult(
    ResumeActivationStatus Status,
    Portfolio.Application.Common.Models.ResumeFile? Resume);

public sealed record ResumeSettings(
    string Bucket,
    long MaxFileSize,
    TimeSpan SignedUrlLifetime);
