using Microsoft.Extensions.Logging;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Localization;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Common.Storage;
using Portfolio.Application.Common.Validation;

namespace Portfolio.Application.Resumes;

public sealed partial class ResumeService(
    IResumeRepository repository,
    IFileStorage storage,
    ResumeSettings settings,
    TimeProvider timeProvider,
    ILogger<ResumeService> logger) : IResumeService
{
    private static readonly TimeZoneInfo BusinessTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public async Task<ResumePublicResponse> GetPublicCurrentAsync(
        string slug,
        string? language,
        CancellationToken cancellationToken)
    {
        var normalizedLanguage = ValidateLanguage(language, required: true);
        var item = await repository.GetPublicCurrentAsync(
            SlugNormalizer.Normalize(slug), normalizedLanguage!, cancellationToken)
            ?? throw new NotFoundException("A current published Resume was not found.");
        var url = await storage.CreateSignedReadUrlAsync(
            settings.Bucket, item.ObjectKey, settings.SignedUrlLifetime, cancellationToken);
        return new ResumePublicResponse(
            item.Language,
            ResumeVersionFormatter.Format(item.VersionYear, item.VersionSequence),
            item.OriginalFileName,
            item.ContentType,
            item.FileSize,
            item.Description,
            url,
            timeProvider.GetUtcNow().Add(settings.SignedUrlLifetime));
    }

    public async Task<ResumeAdminPage> GetResumesAsync(
        ResumeAdminQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page < 1) throw Invalid("page", "Page must be at least 1.");
        if (query.PageSize is < 1 or > 100)
            throw Invalid("pageSize", "Page size must be between 1 and 100.");
        var normalized = query with { Language = ValidateLanguage(query.Language, required: false) };
        var page = await repository.GetResumesAsync(normalized, cancellationToken);
        return new ResumeAdminPage(
            page.Items.Select(MapAdmin).ToArray(),
            page.Total,
            page.Page,
            page.PageSize);
    }

    public async Task<ResumeAdminResponse> UploadAsync(
        ResumeUploadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var language = ValidateLanguage(request);
        var validated = await FileValidation.ValidateAsync(
            new FileUpload(
                request.Content,
                request.OriginalFileName,
                request.ContentType,
                request.Length),
            FileValidationOptions.Pdf(settings.MaxFileSize),
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        var id = Guid.NewGuid();
        var objectKey = $"resumes/{id}/{Guid.NewGuid():N}{validated.Extension}";
        var stored = await storage.UploadAsync(
            new StorageUpload(
                settings.Bucket,
                objectKey,
                request.Content,
                validated.ContentType,
                validated.Length),
            cancellationToken);
        var resume = new ResumeFile
        {
            Id = id,
            LanguageCode = language,
            FileUrl = stored.ObjectKey,
            OriginalFileName = validated.SafeOriginalFileName,
            ContentType = validated.ContentType,
            FileSize = validated.Length,
            IsPublished = request.IsPublished,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        AddTranslation(resume, SupportedLocales.English, request.DescriptionEn);
        AddTranslation(resume, SupportedLocales.Vietnamese, request.DescriptionVi);

        try
        {
            var localNow = TimeZoneInfo.ConvertTime(now, BusinessTimeZone);
            await repository.CreateVersionAsync(resume, checked((short)localNow.Year), cancellationToken);
        }
        catch
        {
            try
            {
                await storage.DeleteIfExistsAsync(
                    stored.Bucket, stored.ObjectKey, CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                LogCleanupFailure(logger, stored.Bucket, stored.ObjectKey, cleanupException);
            }
            throw;
        }

        LogUploaded(logger, resume.Id, resume.LanguageCode, resume.VersionYear, resume.VersionSequence);
        return MapAdmin(resume);
    }

    public async Task<ResumeAdminResponse> SetCurrentAsync(
        Guid id,
        ResumeCurrentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.IsCurrent)
            throw Invalid("isCurrent", "This operation requires isCurrent to be true.");
        var result = await repository.SetCurrentAsync(
            id, timeProvider.GetUtcNow(), cancellationToken);
        return result.Status switch
        {
            ResumeActivationStatus.Activated when result.Resume is not null => MapAdmin(result.Resume),
            ResumeActivationStatus.NotFound => throw new NotFoundException("Resume was not found."),
            ResumeActivationStatus.NotPublished => throw Invalid(
                "isCurrent", "Only a published Resume can be current."),
            _ => throw new InvalidOperationException("The Resume activation result was invalid."),
        };
    }

    public async Task<ResumeAdminResponse> SetPublishedAsync(
        Guid id,
        ResumePublishRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var resume = await repository.GetAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("Resume was not found.");
        if (!request.IsPublished && resume.IsActive)
            throw new ConflictException("An active Resume cannot be unpublished before selecting a replacement.");
        resume.IsPublished = request.IsPublished;
        resume.UpdatedAt = timeProvider.GetUtcNow();
        await repository.SaveChangesAsync(cancellationToken);
        return MapAdmin(resume);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var resume = await repository.GetAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("Resume was not found.");
        if (resume.IsActive)
            throw new ConflictException("An active Resume cannot be deleted before selecting a replacement.");
        var objectKey = resume.FileUrl;
        repository.Remove(resume);
        await repository.SaveChangesAsync(cancellationToken);
        try
        {
            await storage.DeleteIfExistsAsync(settings.Bucket, objectKey, cancellationToken);
        }
        catch (Exception cleanupException)
        {
            LogCleanupFailure(logger, settings.Bucket, objectKey, cleanupException);
        }
        LogDeleted(logger, id);
    }

    private static string ValidateLanguage(ResumeUploadRequest request)
    {
        var language = ValidateLanguage(request.Language, required: true)!;
        if (request.DescriptionEn?.Trim().Length > 500)
            throw Invalid("descriptionEn", "English description must not exceed 500 characters.");
        if (request.DescriptionVi?.Trim().Length > 500)
            throw Invalid("descriptionVi", "Vietnamese description must not exceed 500 characters.");
        if (request.IsActive && !request.IsPublished)
            throw Invalid("isActive", "An active Resume must be published.");
        return language;
    }

    private static string? ValidateLanguage(string? language, bool required)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            if (!required) return null;
            throw Invalid("language", "Language is required and must be either 'en' or 'vi'.");
        }
        if (!SupportedLocales.All.Contains(language))
            throw Invalid("language", "Language must be either 'en' or 'vi'.");
        return language;
    }

    private static void AddTranslation(ResumeFile resume, string locale, string? description) =>
        resume.Translations.Add(new ResumeTranslation
        {
            ResumeId = resume.Id,
            LocaleCode = locale,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
        });

    private static ResumeAdminResponse MapAdmin(ResumeFile resume) =>
        new(
            resume.Id,
            resume.LanguageCode,
            ResumeVersionFormatter.Format(resume.VersionYear, resume.VersionSequence),
            resume.OriginalFileName,
            resume.ContentType,
            resume.FileSize,
            resume.Translations.ToDictionary(
                translation => translation.LocaleCode,
                translation => new ResumeTranslationResponse(translation.Description),
                StringComparer.Ordinal),
            resume.IsPublished,
            resume.IsActive,
            resume.CreatedAt);

    private static ValidationException Invalid(string field, string message) =>
        new("Resume validation failed.", new Dictionary<string, string[]> { [field] = [message] });

    [LoggerMessage(EventId = 2701, Level = LogLevel.Information, Message = "Resume {ResumeId} ({Language} {Year}/{Sequence}) was uploaded.")]
    private static partial void LogUploaded(ILogger logger, Guid resumeId, string language, short year, int sequence);

    [LoggerMessage(EventId = 2702, Level = LogLevel.Information, Message = "Resume {ResumeId} was deleted.")]
    private static partial void LogDeleted(ILogger logger, Guid resumeId);

    [LoggerMessage(EventId = 2703, Level = LogLevel.Error, Message = "Storage cleanup failed for bucket {Bucket} and object {ObjectKey}; reconciliation is required.")]
    private static partial void LogCleanupFailure(ILogger logger, string bucket, string objectKey, Exception exception);
}
