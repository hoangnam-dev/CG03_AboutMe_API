using System.Text.Json.Serialization;

namespace Portfolio.Application.Skills;

public sealed record SkillTranslationRequest(string Name);

public sealed record SkillIconRequest(string Type, string Value);

public sealed record SkillCategoryWriteRequest(
    int DisplayOrder,
    bool IsPublished,
    IReadOnlyDictionary<string, SkillTranslationRequest> Translations);

public sealed record SkillWriteRequest(
    Guid CategoryId,
    string Level,
    decimal? YearsOfExperience,
    SkillIconRequest Icon,
    int DisplayOrder,
    bool IsPublished,
    IReadOnlyDictionary<string, SkillTranslationRequest> Translations);

public sealed record SkillOrderItem(Guid Id, int DisplayOrder);

public sealed record SkillReorderRequest(IReadOnlyList<SkillOrderItem> Items);

public sealed record PublicSkillResponse(
    Guid Id,
    string Name,
    string Level,
    decimal? YearsOfExperience,
    SkillIconRequest Icon,
    int DisplayOrder);

public sealed record PublicSkillCategoryResponse(
    Guid Id,
    string Name,
    int DisplayOrder,
    IReadOnlyList<PublicSkillResponse> Skills);

public sealed record SkillCategoryAdminResponse(
    Guid Id,
    int DisplayOrder,
    bool IsPublished,
    IReadOnlyDictionary<string, SkillTranslationRequest> Translations);

public sealed record SkillAdminResponse(
    Guid Id,
    Guid CategoryId,
    string Level,
    decimal? YearsOfExperience,
    SkillIconRequest Icon,
    int DisplayOrder,
    bool IsPublished,
    IReadOnlyDictionary<string, SkillTranslationRequest> Translations);

public sealed record SkillAdminQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? CategoryId = null,
    bool? IsPublished = null);

public sealed record SkillAdminPage(
    IReadOnlyList<SkillAdminResponse> Items,
    long Total,
    int Page,
    int PageSize)
{
    public int TotalPages => Total == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

public sealed record SkillIconSettings(
    string Bucket,
    long MaxFileSize,
    IReadOnlySet<string> AllowedExternalHosts,
    string? ManagedStorageHost = null);

public sealed record SkillIconUpload(
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long Length);

public sealed record SkillIconUploadResponse(string Url);

public enum SkillReorderResult
{
    Success,
    CategoryNotFound,
    SetMismatch,
}
