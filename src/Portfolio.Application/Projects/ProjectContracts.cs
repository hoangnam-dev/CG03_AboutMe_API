using System.Text.Json.Serialization;
using Portfolio.Application.Common.Models;

namespace Portfolio.Application.Projects;

public sealed record ProjectTranslationRequest(
    string Name,
    string? ShortDescription,
    string? ClientContext,
    string? Role,
    string? Problem,
    string? Solution,
    string? Result);

public sealed record ProjectHighlightRequest(string Locale, string Content, int DisplayOrder);

public sealed record ProjectImageMetadataRequest(
    Guid Id,
    int DisplayOrder,
    IReadOnlyDictionary<string, string> AltText);

public sealed record ProjectWriteRequest(
    string Slug,
    string InternalName,
    ProjectKind Kind,
    ProjectDisclosureLevel DisclosureLevel,
    string? RepositoryUrl,
    string? DemoUrl,
    Guid? ThumbnailImageId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsFeatured,
    int DisplayOrder,
    bool IsPublished,
    IReadOnlyDictionary<string, ProjectTranslationRequest> Translations,
    IReadOnlyList<Guid> TechnologyIds,
    IReadOnlyList<ProjectHighlightRequest> Highlights,
    IReadOnlyList<ProjectImageMetadataRequest> Images);

public sealed record ProjectPublishRequest(bool IsPublished);
public sealed record ProjectOrderItem(Guid Id, int DisplayOrder);
public sealed record ProjectReorderRequest(IReadOnlyList<ProjectOrderItem> Items);

public sealed record ProjectTechnologyResponse(
    Guid Id,
    string Name,
    string IconType,
    string IconValue,
    int DisplayOrder);

public sealed record ProjectHighlightResponse(Guid Id, string Content, int DisplayOrder);
public sealed record ProjectImageResponse(Guid Id, string Url, string AltText, int DisplayOrder);

public sealed record ProjectPublicImageProjection(
    Guid Id,
    string ObjectKey,
    string AltText,
    int DisplayOrder);

public sealed record ProjectPublicProjection(
    Guid Id,
    string Slug,
    string Name,
    string? ShortDescription,
    ProjectKind Kind,
    ProjectDisclosureLevel DisclosureLevel,
    string? ThumbnailObjectKey,
    string? RepositoryUrl,
    string? DemoUrl,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsFeatured,
    string? ClientContext,
    string? Role,
    string? Problem,
    string? Solution,
    string? Result,
    IReadOnlyList<ProjectHighlightResponse> Highlights,
    IReadOnlyList<ProjectTechnologyResponse> Technologies,
    IReadOnlyList<ProjectPublicImageProjection> Images);

public sealed record PublicProjectListItem(
    Guid Id,
    string Slug,
    string Name,
    string? ShortDescription,
    ProjectKind Kind,
    ProjectDisclosureLevel DisclosureLevel,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ThumbnailUrl,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsFeatured,
    string? Role,
    IReadOnlyList<ProjectTechnologyResponse> Technologies);

public sealed record PublicProjectDetail(
    Guid Id,
    string Slug,
    string Name,
    string? ShortDescription,
    ProjectKind Kind,
    ProjectDisclosureLevel DisclosureLevel,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ThumbnailUrl,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsFeatured,
    string? Role,
    IReadOnlyList<ProjectTechnologyResponse> Technologies,
    IReadOnlyList<ProjectHighlightResponse> Highlights,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RepositoryUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DemoUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ClientContext,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Problem,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Solution,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Result,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ProjectImageResponse>? Images);

public sealed record ProjectAdminResponse(
    Guid Id,
    string Slug,
    string InternalName,
    ProjectKind Kind,
    ProjectDisclosureLevel DisclosureLevel,
    string? ThumbnailUrl,
    string? RepositoryUrl,
    string? DemoUrl,
    Guid? ThumbnailImageId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsFeatured,
    int DisplayOrder,
    bool IsPublished,
    IReadOnlyDictionary<string, ProjectTranslationRequest> Translations,
    IReadOnlyList<Guid> TechnologyIds,
    IReadOnlyList<ProjectHighlightRequest> Highlights,
    IReadOnlyList<ProjectImageMetadataResponse> Images);

public sealed record ProjectImageMetadataResponse(
    Guid Id,
    string Url,
    int DisplayOrder,
    IReadOnlyDictionary<string, string> AltText);

public sealed record ProjectAdminQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    ProjectKind? Kind = null,
    bool? IsPublished = null);

public sealed record ProjectAdminPage(
    IReadOnlyList<ProjectAdminResponse> Items,
    long Total,
    int Page,
    int PageSize)
{
    public int TotalPages => Total == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

public sealed record ProjectEntityPage(
    IReadOnlyList<Project> Items,
    long Total,
    int Page,
    int PageSize);

public sealed record ProjectGalleryUpload(
    int FileIndex,
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long Length);

public sealed record ProjectGalleryMetadata(
    int FileIndex,
    int DisplayOrder,
    IReadOnlyDictionary<string, string> AltText);

public sealed record ProjectImageSettings(string Bucket, long MaxFileSize, int MaxFilesPerRequest);

public enum ProjectReorderResult
{
    Success,
    SetMismatch,
}
