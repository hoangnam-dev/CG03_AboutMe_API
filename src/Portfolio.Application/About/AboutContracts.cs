using System.Text.Json.Serialization;
using Portfolio.Application.Common.Models;

namespace Portfolio.Application.About;

public sealed record AboutTranslationRequest(string? Content, string? CareerGoal);

public sealed record AboutUpdateRequest(
    decimal YearsOfExperience,
    int ProjectCount,
    int TechnologyCount,
    bool ShowYearsOfExperience,
    bool ShowProjectCount,
    bool ShowTechnologyCount,
    bool ShowContactSection,
    bool IsPublished,
    IReadOnlyDictionary<string, AboutTranslationRequest> Translations);

public sealed record AboutPublicProjection(
    string? Content,
    string? CareerGoal,
    decimal YearsOfExperience,
    int ProjectCount,
    int TechnologyCount,
    bool ShowYearsOfExperience,
    bool ShowProjectCount,
    bool ShowTechnologyCount,
    bool ShowContactSection);

public sealed record PublicAboutResponse(
    string? Content,
    string? CareerGoal,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? YearsOfExperience,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ProjectCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TechnologyCount,
    bool ShowYearsOfExperience,
    bool ShowProjectCount,
    bool ShowTechnologyCount,
    bool ShowContactSection);

public sealed record AboutAdminResponse(
    Guid Id,
    decimal YearsOfExperience,
    int ProjectCount,
    int TechnologyCount,
    bool ShowYearsOfExperience,
    bool ShowProjectCount,
    bool ShowTechnologyCount,
    bool ShowContactSection,
    bool IsPublished,
    IReadOnlyDictionary<string, AboutTranslationRequest> Translations);
