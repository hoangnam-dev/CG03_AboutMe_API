using Microsoft.Extensions.Logging;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Localization;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Common.Storage;
using Portfolio.Application.Common.Validation;

namespace Portfolio.Application.Skills;

public sealed partial class SkillService(
    ISkillRepository repository,
    SkillIconSettings iconSettings,
    TimeProvider timeProvider,
    ILogger<SkillService> logger,
    IFileStorage storage,
    StorageReplacement storageReplacement) : ISkillService
{
    public async Task<IReadOnlyList<PublicSkillCategoryResponse>> GetPublicAsync(
        string slug, string? locale, CancellationToken cancellationToken) =>
        await repository.GetPublicAsync(
            SlugNormalizer.Normalize(slug), SupportedLocales.Normalize(locale), cancellationToken)
        ?? throw new NotFoundException("Portfolio was not found.");

    public Task<IReadOnlyList<SkillCategoryAdminResponse>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        repository.GetCategoriesAsync(cancellationToken);

    public Task<SkillCategoryAdminResponse> CreateCategoryAsync(
        SkillCategoryWriteRequest request, CancellationToken cancellationToken) =>
        SaveCategoryAsync(null, request, cancellationToken);

    public Task<SkillCategoryAdminResponse> UpdateCategoryAsync(
        Guid id, SkillCategoryWriteRequest request, CancellationToken cancellationToken) =>
        SaveCategoryAsync(id, request, cancellationToken);

    public async Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await repository.GetCategoryForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException("Skill category was not found.");
        if (await repository.CategoryHasSkillsAsync(id, cancellationToken))
            throw new ConflictException("Skill category cannot be deleted while technologies remain.");
        repository.RemoveCategory(category);
        await repository.SaveChangesAsync(cancellationToken);
        LogCategoryDeleted(logger, id);
    }

    public Task<SkillAdminPage> GetSkillsAsync(SkillAdminQuery query, CancellationToken cancellationToken)
    {
        if (query.Page < 1) throw Invalid("page", "Page must be at least 1.");
        if (query.PageSize is < 1 or > 100) throw Invalid("pageSize", "Page size must be between 1 and 100.");
        if (query.Search?.Length > 100) throw Invalid("search", "Search must not exceed 100 characters.");
        return repository.GetSkillsAsync(query with { Search = NullIfWhiteSpace(query.Search) }, cancellationToken);
    }

    public async Task<SkillAdminResponse> GetSkillAsync(Guid id, CancellationToken cancellationToken) =>
        Map(await repository.GetSkillAsync(id, false, cancellationToken)
            ?? throw new NotFoundException("Skill was not found."));

    public Task<SkillAdminResponse> CreateSkillAsync(
        SkillWriteRequest request, CancellationToken cancellationToken) =>
        SaveSkillAsync(null, request, cancellationToken);

    public Task<SkillAdminResponse> UpdateSkillAsync(
        Guid id, SkillWriteRequest request, CancellationToken cancellationToken) =>
        SaveSkillAsync(id, request, cancellationToken);

    public async Task DeleteSkillAsync(Guid id, CancellationToken cancellationToken)
    {
        var skill = await repository.GetSkillAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("Skill was not found.");
        if (await repository.SkillHasReferencesAsync(id, cancellationToken))
            throw new ConflictException("Skill cannot be deleted while professional-history references remain.");
        repository.RemoveSkill(skill);
        await repository.SaveChangesAsync(cancellationToken);
        LogSkillDeleted(logger, id);
    }

    public async Task ReorderSkillsAsync(
        Guid categoryId, SkillReorderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Items.Count == 0) throw Invalid("items", "Reorder items must not be empty.");
        if (request.Items.Any(item => item.DisplayOrder < 0))
            throw Invalid("items.displayOrder", "Display order must be non-negative.");
        if (request.Items.Select(item => item.Id).Distinct().Count() != request.Items.Count)
            throw Invalid("items.id", "Skill IDs must be unique.");
        if (request.Items.Select(item => item.DisplayOrder).Distinct().Count() != request.Items.Count)
            throw Invalid("items.displayOrder", "Display orders must be unique.");

        var result = await repository.ReorderSkillsAsync(categoryId, request.Items, cancellationToken);
        if (result == SkillReorderResult.CategoryNotFound)
            throw new NotFoundException("Skill category was not found.");
        if (result == SkillReorderResult.SetMismatch)
            throw new ConflictException("Reorder must contain the category's complete current skill set.");
        LogSkillsReordered(logger, categoryId, request.Items.Count);
    }

    public async Task<SkillIconUploadResponse> ReplaceIconAsync(
        Guid id, SkillIconUpload upload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        var skill = await repository.GetSkillAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("Skill was not found.");
        var validated = await FileValidation.ValidateAsync(
            new FileUpload(upload.Content, upload.OriginalFileName, upload.ContentType, upload.Length),
            FileValidationOptions.SkillIcons(iconSettings.MaxFileSize),
            cancellationToken);
        if (validated.Kind == FileKind.Svg)
            await SvgValidation.EnsureSafeAsync(upload.Content, cancellationToken);

        var objectKey = $"skills/{skill.Id:D}/{Guid.NewGuid():N}{validated.Extension}";
        var oldObjectKey = skill.IconType == SkillIconType.Image
            ? TryGetManagedObjectKey(skill.IconValue)
            : null;
        var stored = await storageReplacement.ReplaceAsync(
            new StorageUpload(
                iconSettings.Bucket, objectKey, upload.Content, validated.ContentType, validated.Length),
            oldObjectKey,
            async (newObject, token) =>
            {
                skill.IconType = SkillIconType.Image;
                skill.IconValue = storage.GetPublicReadUrl(newObject.Bucket, newObject.ObjectKey).AbsoluteUri;
                skill.UpdatedAt = timeProvider.GetUtcNow();
                await repository.SaveChangesAsync(token);
            },
            cancellationToken);
        var url = storage.GetPublicReadUrl(stored.Bucket, stored.ObjectKey).AbsoluteUri;
        LogIconReplaced(logger, skill.Id);
        return new SkillIconUploadResponse(url);
    }

    private async Task<SkillCategoryAdminResponse> SaveCategoryAsync(
        Guid? id, SkillCategoryWriteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateTranslations(request.Translations, request.IsPublished);
        if (request.DisplayOrder < 0) throw Invalid("displayOrder", "Display order must be non-negative.");

        var category = id.HasValue
            ? await repository.GetCategoryForUpdateAsync(id.Value, cancellationToken)
                ?? throw new NotFoundException("Skill category was not found.")
            : new SkillCategory { Id = Guid.NewGuid(), CreatedAt = timeProvider.GetUtcNow() };
        foreach (var pair in request.Translations)
            if (await repository.CategoryNameExistsAsync(category.Id, pair.Key, pair.Value.Name.Trim(), id, cancellationToken))
                throw new ConflictException($"Skill category name already exists for locale '{pair.Key}'.");

        category.DisplayOrder = request.DisplayOrder;
        category.IsPublished = request.IsPublished;
        category.UpdatedAt = timeProvider.GetUtcNow();
        ReplaceCategoryTranslations(category, request.Translations);
        if (!id.HasValue) await repository.AddCategoryAsync(category, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        LogCategorySaved(logger, category.Id, id.HasValue ? "updated" : "created");
        return Map(category);
    }

    private async Task<SkillAdminResponse> SaveSkillAsync(
        Guid? id, SkillWriteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateTranslations(request.Translations, request.IsPublished);
        if (!Enum.TryParse<SkillLevel>(request.Level, false, out var level) || !Enum.IsDefined(level))
            throw Invalid("level", "Level must be Primary, Experienced, Familiar, or Learning.");
        if (request.YearsOfExperience < 0) throw Invalid("yearsOfExperience", "Years of experience must be non-negative.");
        if (request.DisplayOrder < 0) throw Invalid("displayOrder", "Display order must be non-negative.");
        var icon = SkillIconValidator.Validate(request.Icon, iconSettings);
        var category = await repository.GetCategoryForUpdateAsync(request.CategoryId, cancellationToken)
            ?? throw Invalid("categoryId", "Category does not exist.");
        if (request.IsPublished && !category.IsPublished)
            throw Invalid("categoryId", "A published skill requires a published category.");

        var skill = id.HasValue
            ? await repository.GetSkillAsync(id.Value, true, cancellationToken)
                ?? throw new NotFoundException("Skill was not found.")
            : new Technology { Id = Guid.NewGuid(), CreatedAt = timeProvider.GetUtcNow() };
        foreach (var pair in request.Translations)
            if (await repository.SkillNameExistsAsync(request.CategoryId, pair.Key, pair.Value.Name.Trim(), id, cancellationToken))
                throw new ConflictException($"Skill name already exists for locale '{pair.Key}'.");

        skill.CategoryId = request.CategoryId;
        skill.SkillLevel = level;
        skill.YearsOfExperience = request.YearsOfExperience;
        skill.IconType = Enum.Parse<SkillIconType>(icon.Type, true);
        skill.IconValue = icon.Value;
        skill.DisplayOrder = request.DisplayOrder;
        skill.IsPublished = request.IsPublished;
        skill.UpdatedAt = timeProvider.GetUtcNow();
        ReplaceSkillTranslations(skill, request.Translations);
        if (!id.HasValue) await repository.AddSkillAsync(skill, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        LogSkillSaved(logger, skill.Id, id.HasValue ? "updated" : "created");
        return Map(skill);
    }

    private static void ValidateTranslations(
        IReadOnlyDictionary<string, SkillTranslationRequest> translations, bool publishing)
    {
        if (translations.Keys.Any(locale => !SupportedLocales.All.Contains(locale)))
            throw Invalid("translations", "Only 'en' and 'vi' translations are supported.");
        foreach (var pair in translations)
            if (string.IsNullOrWhiteSpace(pair.Value.Name) || pair.Value.Name.Trim().Length > 100)
                throw Invalid($"translations.{pair.Key}.name", "Name is required and must not exceed 100 characters.");
        if (publishing) PublishTranslationValidation.EnsureComplete(translations.Keys);
    }

    private static void ReplaceCategoryTranslations(
        SkillCategory category, IReadOnlyDictionary<string, SkillTranslationRequest> translations)
    {
        foreach (var obsolete in category.Translations.Where(item => !translations.ContainsKey(item.LocaleCode)).ToArray())
            category.Translations.Remove(obsolete);
        foreach (var pair in translations)
        {
            var target = category.Translations.SingleOrDefault(item => item.LocaleCode == pair.Key);
            if (target is null)
            {
                target = new SkillCategoryTranslation { CategoryId = category.Id, LocaleCode = pair.Key };
                category.Translations.Add(target);
            }
            target.Name = pair.Value.Name.Trim();
        }
    }

    private static void ReplaceSkillTranslations(
        Technology skill, IReadOnlyDictionary<string, SkillTranslationRequest> translations)
    {
        foreach (var obsolete in skill.Translations.Where(item => !translations.ContainsKey(item.LocaleCode)).ToArray())
            skill.Translations.Remove(obsolete);
        foreach (var pair in translations)
        {
            var target = skill.Translations.SingleOrDefault(item => item.LocaleCode == pair.Key);
            if (target is null)
            {
                target = new TechnologyTranslation { TechnologyId = skill.Id, LocaleCode = pair.Key };
                skill.Translations.Add(target);
            }
            target.Name = pair.Value.Name.Trim();
        }
    }

    private static SkillCategoryAdminResponse Map(SkillCategory category) => new(
        category.Id, category.DisplayOrder, category.IsPublished,
        category.Translations.ToDictionary(
            item => item.LocaleCode, item => new SkillTranslationRequest(item.Name), StringComparer.Ordinal));

    private static SkillAdminResponse Map(Technology skill) => new(
        skill.Id, skill.CategoryId, skill.SkillLevel.ToString(), skill.YearsOfExperience,
        new SkillIconRequest(skill.IconType.ToString().ToLowerInvariant(), skill.IconValue),
        skill.DisplayOrder, skill.IsPublished,
        skill.Translations.ToDictionary(
            item => item.LocaleCode, item => new SkillTranslationRequest(item.Name), StringComparer.Ordinal));

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private string? TryGetManagedObjectKey(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;
        var marker = $"/storage/v1/object/public/{iconSettings.Bucket}/";
        return uri.AbsolutePath.StartsWith(marker, StringComparison.Ordinal)
            ? Uri.UnescapeDataString(uri.AbsolutePath[marker.Length..])
            : null;
    }
    private static ValidationException Invalid(string field, string message) =>
        new("Skill validation failed.", new Dictionary<string, string[]> { [field] = [message] });

    [LoggerMessage(EventId = 3201, Level = LogLevel.Information, Message = "Skill category {CategoryId} {Operation}.")]
    private static partial void LogCategorySaved(ILogger logger, Guid categoryId, string operation);
    [LoggerMessage(EventId = 3202, Level = LogLevel.Information, Message = "Skill category {CategoryId} deleted.")]
    private static partial void LogCategoryDeleted(ILogger logger, Guid categoryId);
    [LoggerMessage(EventId = 3203, Level = LogLevel.Information, Message = "Skill {SkillId} {Operation}.")]
    private static partial void LogSkillSaved(ILogger logger, Guid skillId, string operation);
    [LoggerMessage(EventId = 3204, Level = LogLevel.Information, Message = "Skill {SkillId} deleted.")]
    private static partial void LogSkillDeleted(ILogger logger, Guid skillId);
    [LoggerMessage(EventId = 3205, Level = LogLevel.Information, Message = "Reordered {Count} skills in category {CategoryId}.")]
    private static partial void LogSkillsReordered(ILogger logger, Guid categoryId, int count);
    [LoggerMessage(EventId = 3206, Level = LogLevel.Information, Message = "Skill {SkillId} icon replaced.")]
    private static partial void LogIconReplaced(ILogger logger, Guid skillId);
}
