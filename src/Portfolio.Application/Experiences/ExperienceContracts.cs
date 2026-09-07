namespace Portfolio.Application.Experiences;

public sealed record ExperienceTranslationRequest(
    string Position,
    string? Location,
    string? Description);

public sealed record ExperienceHighlightRequest(
    string Locale,
    string HighlightType,
    string Content,
    int DisplayOrder);

public sealed record ExperienceWriteRequest(
    string CompanyName,
    string? EmploymentType,
    string? CompanyUrl,
    DateOnly StartDate,
    DateOnly? EndDate,
    int DisplayOrder,
    bool IsPublished,
    IReadOnlyDictionary<string, ExperienceTranslationRequest> Translations,
    IReadOnlyList<ExperienceHighlightRequest> Highlights,
    IReadOnlyList<Guid> TechnologyIds);

public sealed record ExperienceOrderItem(Guid Id, int DisplayOrder);

public sealed record ExperienceReorderRequest(IReadOnlyList<ExperienceOrderItem> Items);

public sealed record ExperienceHighlightResponse(
    Guid Id,
    string Locale,
    string HighlightType,
    string Content,
    int DisplayOrder);

public sealed record ExperienceTechnologyResponse(
    Guid Id,
    string Name,
    string IconType,
    string IconValue,
    int DisplayOrder);

public sealed record PublicExperienceResponse(
    Guid Id,
    string CompanyName,
    string? EmploymentType,
    string? CompanyUrl,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsCurrent,
    int DisplayOrder,
    ExperienceTranslationRequest Translation,
    IReadOnlyList<ExperienceHighlightResponse> Highlights,
    IReadOnlyList<ExperienceTechnologyResponse> Technologies);

public sealed record ExperienceAdminResponse(
    Guid Id,
    string CompanyName,
    string? EmploymentType,
    string? CompanyUrl,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsCurrent,
    int DisplayOrder,
    bool IsPublished,
    IReadOnlyDictionary<string, ExperienceTranslationRequest> Translations,
    IReadOnlyList<ExperienceHighlightResponse> Highlights,
    IReadOnlyList<Guid> TechnologyIds);

public sealed record ExperienceAdminQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool? IsPublished = null);

public sealed record ExperienceAdminPage(
    IReadOnlyList<ExperienceAdminResponse> Items,
    long Total,
    int Page,
    int PageSize)
{
    public int TotalPages => Total == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

public enum ExperienceReorderResult
{
    Success,
    SetMismatch,
}
