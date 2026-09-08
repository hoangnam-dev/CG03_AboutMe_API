using System.Text.Json.Serialization;

namespace Portfolio.Application.Certificates;

public sealed record CertificateTranslationRequest(string Name);

public sealed record CertificateWriteRequest(
    string Issuer,
    DateOnly IssuedDate,
    DateOnly? ExpirationDate,
    string? CredentialId,
    bool ShowCredentialId,
    string? CredentialUrl,
    int DisplayOrder,
    bool IsPublished,
    IReadOnlyDictionary<string, CertificateTranslationRequest> Translations,
    IReadOnlyList<Guid> TechnologyIds);

public sealed record CertificateTechnologyResponse(
    Guid Id,
    string Name,
    string IconType,
    string IconValue,
    int DisplayOrder);

public sealed record CertificatePublicProjection(
    Guid Id,
    string Issuer,
    DateOnly IssuedDate,
    DateOnly? ExpirationDate,
    string? CredentialId,
    bool ShowCredentialId,
    string? CredentialUrl,
    int DisplayOrder,
    string Name,
    string? FileObjectKey,
    string? ImageObjectKey,
    IReadOnlyList<CertificateTechnologyResponse> Technologies);

public sealed record CertificatePublicResponse(
    Guid Id,
    string Issuer,
    DateOnly IssuedDate,
    DateOnly? ExpirationDate,
    bool IsExpired,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CredentialId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CredentialUrl,
    int DisplayOrder,
    string Name,
    IReadOnlyList<CertificateTechnologyResponse> Technologies,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Uri? DownloadUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? DownloadUrlExpiresAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Uri? ImageUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? ImageUrlExpiresAt);

public sealed record CertificateAdminResponse(
    Guid Id,
    string Issuer,
    DateOnly IssuedDate,
    DateOnly? ExpirationDate,
    bool IsExpired,
    string? CredentialId,
    bool ShowCredentialId,
    string? CredentialUrl,
    int DisplayOrder,
    bool IsPublished,
    IReadOnlyDictionary<string, CertificateTranslationRequest> Translations,
    IReadOnlyList<Guid> TechnologyIds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Uri? DownloadUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? DownloadUrlExpiresAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Uri? ImageUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? ImageUrlExpiresAt);

public sealed record CertificateAdminQuery(
    int Page,
    int PageSize,
    string? Search,
    bool? IsPublished);

public sealed record CertificateEntityPage(
    IReadOnlyList<Portfolio.Application.Common.Models.Certificate> Items,
    long Total,
    int Page,
    int PageSize);

public sealed record CertificateAdminPage(
    IReadOnlyList<CertificateAdminResponse> Items,
    long Total,
    int Page,
    int PageSize)
{
    public int TotalPages => Total == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

public sealed record CertificateEvidenceUpload(
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long Length);

public sealed record CertificateEvidenceResponse(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Uri? DownloadUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? DownloadUrlExpiresAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Uri? ImageUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? ImageUrlExpiresAt);

public sealed record CertificateEvidenceSettings(
    string Bucket,
    long MaxFileSize,
    TimeSpan SignedUrlLifetime);
