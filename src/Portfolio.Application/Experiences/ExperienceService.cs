using Microsoft.Extensions.Logging;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Localization;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Common.Validation;

namespace Portfolio.Application.Experiences;

public sealed partial class ExperienceService(
    IExperienceRepository repository,
    TimeProvider timeProvider,
    ILogger<ExperienceService> logger) : IExperienceService
{
    public async Task<IReadOnlyList<PublicExperienceResponse>> GetPublicAsync(
        string slug, string? locale, CancellationToken cancellationToken) =>
        await repository.GetPublicAsync(
            SlugNormalizer.Normalize(slug), SupportedLocales.Normalize(locale), cancellationToken)
        ?? throw new NotFoundException("Portfolio was not found.");

    public Task<ExperienceAdminPage> GetExperiencesAsync(
        ExperienceAdminQuery query, CancellationToken cancellationToken)
    {
        if (query.Page < 1) throw Invalid("page", "Page must be at least 1.");
        if (query.PageSize is < 1 or > 100)
            throw Invalid("pageSize", "Page size must be between 1 and 100.");
        if (query.Search?.Length > 200)
            throw Invalid("search", "Search must not exceed 200 characters.");
        return repository.GetExperiencesAsync(
            query with { Search = NullIfWhiteSpace(query.Search) }, cancellationToken);
    }

    public async Task<ExperienceAdminResponse> GetExperienceAsync(
        Guid id, CancellationToken cancellationToken) =>
        Map(await repository.GetAsync(id, false, cancellationToken)
            ?? throw new NotFoundException("Work experience was not found."));

    public Task<ExperienceAdminResponse> CreateExperienceAsync(
        ExperienceWriteRequest request, CancellationToken cancellationToken) =>
        SaveAsync(null, request, cancellationToken);

    public Task<ExperienceAdminResponse> UpdateExperienceAsync(
        Guid id, ExperienceWriteRequest request, CancellationToken cancellationToken) =>
        SaveAsync(id, request, cancellationToken);

    public async Task DeleteExperienceAsync(Guid id, CancellationToken cancellationToken)
    {
        var experience = await repository.GetAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("Work experience was not found.");
        repository.Remove(experience);
        await repository.SaveChangesAsync(cancellationToken);
        LogDeleted(logger, id);
    }

    public async Task ReorderExperiencesAsync(
        ExperienceReorderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Items is null || request.Items.Count == 0)
            throw Invalid("items", "Reorder items must not be empty.");
        if (request.Items.Any(item => item.DisplayOrder < 0))
            throw Invalid("items.displayOrder", "Display order must be non-negative.");
        if (request.Items.Select(item => item.Id).Distinct().Count() != request.Items.Count)
            throw Invalid("items.id", "Experience IDs must be unique.");
        if (request.Items.Select(item => item.DisplayOrder).Distinct().Count() != request.Items.Count)
            throw Invalid("items.displayOrder", "Display orders must be unique.");

        if (await repository.ReorderAsync(request.Items, cancellationToken) == ExperienceReorderResult.SetMismatch)
            throw new ConflictException("Reorder must contain the complete current experience set.");
        LogReordered(logger, request.Items.Count);
    }

    private async Task<ExperienceAdminResponse> SaveAsync(
        Guid? id, ExperienceWriteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        if (!await repository.TechnologyIdsExistAsync(
                request.TechnologyIds.Distinct().ToArray(), cancellationToken))
            throw Invalid("technologyIds", "Every technology ID must exist.");

        var experience = id.HasValue
            ? await repository.GetAsync(id.Value, true, cancellationToken)
                ?? throw new NotFoundException("Work experience was not found.")
            : new WorkExperience { Id = Guid.NewGuid(), CreatedAt = timeProvider.GetUtcNow() };

        experience.CompanyName = request.CompanyName.Trim();
        experience.EmploymentType = NullIfWhiteSpace(request.EmploymentType);
        experience.CompanyUrl = NullIfWhiteSpace(request.CompanyUrl);
        experience.StartDate = request.StartDate;
        experience.EndDate = request.EndDate;
        experience.DisplayOrder = request.DisplayOrder;
        experience.IsPublished = request.IsPublished;
        experience.UpdatedAt = timeProvider.GetUtcNow();
        ReplaceTranslations(experience, request.Translations);
        ReplaceHighlights(experience, request.Highlights);
        ReplaceTechnologies(experience, request.TechnologyIds);

        if (!id.HasValue) await repository.AddAsync(experience, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        LogSaved(logger, experience.Id, id.HasValue ? "updated" : "created");
        return Map(experience);
    }

    private static void Validate(ExperienceWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyName) || request.CompanyName.Trim().Length > 200)
            throw Invalid("companyName", "Company name is required and must not exceed 200 characters.");
        if (request.EmploymentType?.Trim().Length > 30)
            throw Invalid("employmentType", "Employment type must not exceed 30 characters.");
        if (request.CompanyUrl is not null &&
            (!Uri.TryCreate(request.CompanyUrl.Trim(), UriKind.Absolute, out var companyUri) ||
             companyUri.Scheme != Uri.UriSchemeHttps))
            throw Invalid("companyUrl", "Company URL must be an absolute HTTPS URL.");
        if (request.StartDate == default)
            throw Invalid("startDate", "Start date is required.");
        if (request.EndDate < request.StartDate)
            throw Invalid("endDate", "End date must be on or after start date.");
        if (request.DisplayOrder < 0)
            throw Invalid("displayOrder", "Display order must be non-negative.");
        if (request.Translations is null)
            throw Invalid("translations", "Translations are required.");
        if (request.Highlights is null)
            throw Invalid("highlights", "Highlights are required.");
        if (request.TechnologyIds is null)
            throw Invalid("technologyIds", "Technology IDs are required.");

        ValidateTranslations(request.Translations, request.IsPublished);
        ValidateHighlights(request.Highlights);
        if (request.TechnologyIds.Distinct().Count() != request.TechnologyIds.Count)
            throw Invalid("technologyIds", "Technology IDs must be unique.");
    }

    private static void ValidateTranslations(
        IReadOnlyDictionary<string, ExperienceTranslationRequest> translations, bool publishing)
    {
        if (translations.Keys.Any(locale => !SupportedLocales.All.Contains(locale)))
            throw Invalid("translations", "Only 'en' and 'vi' translations are supported.");
        foreach (var pair in translations)
        {
            if (pair.Value is null || string.IsNullOrWhiteSpace(pair.Value.Position) ||
                pair.Value.Position.Trim().Length > 150)
                throw Invalid($"translations.{pair.Key}.position",
                    "Position is required and must not exceed 150 characters.");
            if (pair.Value.Location?.Trim().Length > 200)
                throw Invalid($"translations.{pair.Key}.location",
                    "Location must not exceed 200 characters.");
        }
        if (publishing) PublishTranslationValidation.EnsureComplete(translations.Keys);
    }

    private static void ValidateHighlights(IReadOnlyList<ExperienceHighlightRequest> highlights)
    {
        foreach (var item in highlights)
        {
            if (!SupportedLocales.All.Contains(item.Locale))
                throw Invalid("highlights.locale", "Highlight locale must be 'en' or 'vi'.");
            if (ParseHighlightType(item.HighlightType) is null)
                throw Invalid("highlights.highlightType",
                    "Highlight type must be 'responsibility' or 'achievement'.");
            if (string.IsNullOrWhiteSpace(item.Content))
                throw Invalid("highlights.content", "Highlight content is required.");
            if (item.DisplayOrder < 0)
                throw Invalid("highlights.displayOrder", "Highlight display order must be non-negative.");
        }

        if (highlights.GroupBy(item => (item.Locale, item.HighlightType.ToLowerInvariant(), item.DisplayOrder))
            .Any(group => group.Count() > 1))
            throw Invalid("highlights.displayOrder",
                "Highlight display order must be unique within each locale and highlight type.");
    }

    private static void ReplaceTranslations(
        WorkExperience experience,
        IReadOnlyDictionary<string, ExperienceTranslationRequest> translations)
    {
        foreach (var obsolete in experience.Translations
            .Where(item => !translations.ContainsKey(item.LocaleCode)).ToArray())
            experience.Translations.Remove(obsolete);
        foreach (var pair in translations)
        {
            var target = experience.Translations.SingleOrDefault(item => item.LocaleCode == pair.Key);
            if (target is null)
            {
                target = new ExperienceTranslation
                {
                    ExperienceId = experience.Id,
                    LocaleCode = pair.Key,
                };
                experience.Translations.Add(target);
            }
            target.Position = pair.Value.Position.Trim();
            target.Location = NullIfWhiteSpace(pair.Value.Location);
            target.Description = NullIfWhiteSpace(pair.Value.Description);
        }
    }

    private static void ReplaceHighlights(
        WorkExperience experience, IReadOnlyList<ExperienceHighlightRequest> highlights)
    {
        var requestedKeys = highlights
            .Select(item => (item.Locale, Type: ParseHighlightType(item.HighlightType)!.Value, item.DisplayOrder))
            .ToHashSet();
        foreach (var obsolete in experience.Highlights.Where(item =>
            !requestedKeys.Contains((item.LocaleCode, item.HighlightType, item.DisplayOrder))).ToArray())
            experience.Highlights.Remove(obsolete);

        foreach (var item in highlights)
        {
            var type = ParseHighlightType(item.HighlightType)!.Value;
            var target = experience.Highlights.SingleOrDefault(existing =>
                existing.LocaleCode == item.Locale &&
                existing.HighlightType == type &&
                existing.DisplayOrder == item.DisplayOrder);
            if (target is null)
            {
                target = new ExperienceHighlight
                {
                    Id = Guid.NewGuid(),
                    ExperienceId = experience.Id,
                    LocaleCode = item.Locale,
                    HighlightType = type,
                    DisplayOrder = item.DisplayOrder,
                };
                experience.Highlights.Add(target);
            }
            target.Content = item.Content.Trim();
        }
    }

    private static void ReplaceTechnologies(
        WorkExperience experience, IReadOnlyList<Guid> technologyIds)
    {
        var requestedIds = technologyIds.ToHashSet();
        foreach (var obsolete in experience.Technologies
            .Where(item => !requestedIds.Contains(item.TechnologyId)).ToArray())
            experience.Technologies.Remove(obsolete);

        for (var index = 0; index < technologyIds.Count; index++)
        {
            var target = experience.Technologies.SingleOrDefault(
                item => item.TechnologyId == technologyIds[index]);
            if (target is null)
            {
                target = new ExperienceTechnology
                {
                    ExperienceId = experience.Id,
                    TechnologyId = technologyIds[index],
                };
                experience.Technologies.Add(target);
            }
            target.DisplayOrder = index;
        }
    }

    internal static ExperienceAdminResponse Map(WorkExperience experience) => new(
        experience.Id,
        experience.CompanyName,
        experience.EmploymentType,
        experience.CompanyUrl,
        experience.StartDate,
        experience.EndDate,
        experience.EndDate is null,
        experience.DisplayOrder,
        experience.IsPublished,
        experience.Translations.ToDictionary(
            item => item.LocaleCode,
            item => new ExperienceTranslationRequest(item.Position, item.Location, item.Description),
            StringComparer.Ordinal),
        experience.Highlights
            .OrderBy(item => item.LocaleCode)
            .ThenBy(item => item.HighlightType)
            .ThenBy(item => item.DisplayOrder)
            .Select(item => new ExperienceHighlightResponse(
                item.Id,
                item.LocaleCode,
                item.HighlightType.ToString().ToLowerInvariant(),
                item.Content,
                item.DisplayOrder))
            .ToArray(),
        experience.Technologies
            .OrderBy(item => item.DisplayOrder)
            .Select(item => item.TechnologyId)
            .ToArray());

    private static ExperienceHighlightType? ParseHighlightType(string? value) => value switch
    {
        "responsibility" => ExperienceHighlightType.Responsibility,
        "achievement" => ExperienceHighlightType.Achievement,
        _ => null,
    };

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ValidationException Invalid(string field, string message) =>
        new("Experience validation failed.", new Dictionary<string, string[]> { [field] = [message] });

    [LoggerMessage(EventId = 3301, Level = LogLevel.Information, Message = "Work experience {ExperienceId} {Operation}.")]
    private static partial void LogSaved(ILogger logger, Guid experienceId, string operation);
    [LoggerMessage(EventId = 3302, Level = LogLevel.Information, Message = "Work experience {ExperienceId} deleted.")]
    private static partial void LogDeleted(ILogger logger, Guid experienceId);
    [LoggerMessage(EventId = 3303, Level = LogLevel.Information, Message = "Reordered {Count} work experiences.")]
    private static partial void LogReordered(ILogger logger, int count);
}
