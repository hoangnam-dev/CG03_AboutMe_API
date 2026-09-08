using Microsoft.Extensions.Logging;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Localization;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Common.Storage;
using Portfolio.Application.Common.Validation;

namespace Portfolio.Application.Projects;

public sealed partial class ProjectService(
    IProjectRepository repository,
    IFileStorage storage,
    ProjectImageSettings imageSettings,
    TimeProvider timeProvider,
    ILogger<ProjectService> logger) : IProjectService
{
    public async Task<IReadOnlyList<PublicProjectListItem>> GetPublicListAsync(
        string slug, string? locale, CancellationToken cancellationToken)
    {
        var projects = await repository.GetPublicListAsync(
            SlugNormalizer.Normalize(slug), SupportedLocales.Normalize(locale), cancellationToken)
            ?? throw new NotFoundException("Portfolio was not found.");
        return projects.Select(MapList).ToArray();
    }

    public async Task<PublicProjectDetail> GetPublicDetailAsync(
        string slug, string projectSlug, string? locale, CancellationToken cancellationToken)
    {
        var project = await repository.GetPublicDetailAsync(
            SlugNormalizer.Normalize(slug),
            SlugNormalizer.Normalize(projectSlug),
            SupportedLocales.Normalize(locale),
            cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        return MapDetail(project);
    }

    public async Task<ProjectAdminPage> GetProjectsAsync(
        ProjectAdminQuery query, CancellationToken cancellationToken)
    {
        if (query.Page < 1) throw Invalid("page", "Page must be at least 1.");
        if (query.PageSize is < 1 or > 100)
            throw Invalid("pageSize", "Page size must be between 1 and 100.");
        if (query.Search?.Length > 200)
            throw Invalid("search", "Search must not exceed 200 characters.");
        var page = await repository.GetProjectsAsync(
            query with { Search = NullIfWhiteSpace(query.Search) }, cancellationToken);
        return new ProjectAdminPage(
            page.Items.Select(MapAdmin).ToArray(), page.Total, page.Page, page.PageSize);
    }

    public async Task<ProjectAdminResponse> GetProjectAsync(Guid id, CancellationToken cancellationToken) =>
        MapAdmin(await repository.GetAsync(id, false, cancellationToken)
            ?? throw new NotFoundException("Project was not found."));

    public Task<ProjectAdminResponse> CreateProjectAsync(
        ProjectWriteRequest request, CancellationToken cancellationToken) =>
        SaveAsync(null, request, cancellationToken);

    public Task<ProjectAdminResponse> UpdateProjectAsync(
        Guid id, ProjectWriteRequest request, CancellationToken cancellationToken) =>
        SaveAsync(id, request, cancellationToken);

    public async Task DeleteProjectAsync(Guid id, CancellationToken cancellationToken)
    {
        var project = await repository.GetAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        var objectKeys = project.Images.Select(image => image.ImageUrl).ToArray();
        repository.Remove(project);
        await repository.SaveChangesAsync(cancellationToken);
        foreach (var objectKey in objectKeys)
            await storage.DeleteIfExistsAsync(imageSettings.Bucket, objectKey, cancellationToken);
        LogDeleted(logger, id);
    }

    public async Task<ProjectAdminResponse> SetPublishedAsync(
        Guid id, ProjectPublishRequest request, CancellationToken cancellationToken)
    {
        var project = await repository.GetAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");
        if (request.IsPublished) ValidatePublishing(project);
        project.IsPublished = request.IsPublished;
        project.UpdatedAt = timeProvider.GetUtcNow();
        await repository.SaveChangesAsync(cancellationToken);
        LogPublished(logger, id, request.IsPublished);
        return MapAdmin(project);
    }

    public async Task ReorderProjectsAsync(
        ProjectReorderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Items is null || request.Items.Count == 0)
            throw Invalid("items", "Reorder items must not be empty.");
        if (request.Items.Any(item => item.DisplayOrder < 0))
            throw Invalid("items.displayOrder", "Display order must be non-negative.");
        if (request.Items.Select(item => item.Id).Distinct().Count() != request.Items.Count)
            throw Invalid("items.id", "Project IDs must be unique.");
        if (request.Items.Select(item => item.DisplayOrder).Distinct().Count() != request.Items.Count)
            throw Invalid("items.displayOrder", "Display orders must be unique.");
        if (await repository.ReorderAsync(request.Items, cancellationToken) == ProjectReorderResult.SetMismatch)
            throw new ConflictException("Reorder must contain the complete current project set.");
        LogReordered(logger, request.Items.Count);
    }

    public async Task<IReadOnlyList<ProjectImageMetadataResponse>> UploadGalleryAsync(
        Guid id,
        IReadOnlyList<ProjectGalleryUpload> files,
        IReadOnlyList<ProjectGalleryMetadata> metadata,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(metadata);
        ValidateGalleryContract(files, metadata, imageSettings.MaxFilesPerRequest);
        var project = await repository.GetAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        var validated = new List<(ProjectGalleryUpload File, ProjectGalleryMetadata Metadata, ValidatedFile Validated)>();
        foreach (var file in files)
        {
            var value = await FileValidation.ValidateAsync(
                new FileUpload(file.Content, file.OriginalFileName, file.ContentType, file.Length),
                FileValidationOptions.Images(imageSettings.MaxFileSize),
                cancellationToken);
            validated.Add((file, metadata.Single(item => item.FileIndex == file.FileIndex), value));
        }

        var uploaded = new List<StorageObject>();
        var added = new List<ProjectImage>();
        try
        {
            foreach (var item in validated.OrderBy(item => item.File.FileIndex))
            {
                var imageId = Guid.NewGuid();
                var objectKey = $"projects/{project.Id}/{Guid.NewGuid():N}{item.Validated.Extension}";
                var stored = await storage.UploadAsync(
                    new StorageUpload(
                        imageSettings.Bucket,
                        objectKey,
                        item.File.Content,
                        item.Validated.ContentType,
                        item.Validated.Length),
                    cancellationToken);
                uploaded.Add(stored);
                var image = new ProjectImage
                {
                    Id = imageId,
                    ProjectId = project.Id,
                    ImageUrl = stored.ObjectKey,
                    DisplayOrder = item.Metadata.DisplayOrder,
                    CreatedAt = timeProvider.GetUtcNow(),
                    UpdatedAt = timeProvider.GetUtcNow(),
                };
                foreach (var alt in item.Metadata.AltText)
                    image.Translations.Add(new ProjectImageTranslation
                    {
                        ProjectImageId = imageId,
                        LocaleCode = alt.Key,
                        AltText = alt.Value.Trim(),
                    });
                added.Add(image);
            }

            foreach (var image in added) project.Images.Add(image);
            project.UpdatedAt = timeProvider.GetUtcNow();
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            foreach (var image in added) project.Images.Remove(image);
            await CompensateAsync(uploaded);
            throw;
        }

        LogGalleryUploaded(logger, project.Id, added.Count);
        return added.OrderBy(image => image.DisplayOrder).ThenBy(image => image.Id)
            .Select(MapImageAdmin).ToArray();
    }

    private async Task<ProjectAdminResponse> SaveAsync(
        Guid? id, ProjectWriteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        var slug = SlugNormalizer.Normalize(request.Slug);
        if (await repository.SlugExistsAsync(slug, id, cancellationToken))
            throw new ConflictException("Project slug already exists.");
        if (!await repository.TechnologyIdsExistAsync(request.TechnologyIds, cancellationToken))
            throw Invalid("technologyIds", "Every technology ID must exist.");

        var project = id.HasValue
            ? await repository.GetAsync(id.Value, true, cancellationToken)
                ?? throw new NotFoundException("Project was not found.")
            : new Project { Id = Guid.NewGuid(), CreatedAt = timeProvider.GetUtcNow() };
        if (!id.HasValue && request.Images.Count != 0)
            throw Invalid("images", "Gallery image metadata cannot be supplied before files are uploaded.");
        ValidateImageMetadata(project, request.Images, request.ThumbnailImageId);

        var removedKeys = ReplaceImageMetadata(project, request.Images);
        project.Slug = slug;
        project.InternalName = request.InternalName.Trim();
        project.Kind = request.Kind;
        project.DisclosureLevel = request.DisclosureLevel;
        project.RepositoryUrl = NullIfWhiteSpace(request.RepositoryUrl);
        project.DemoUrl = NullIfWhiteSpace(request.DemoUrl);
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;
        project.IsFeatured = request.IsFeatured;
        project.DisplayOrder = request.DisplayOrder;
        project.IsPublished = request.IsPublished;
        project.ThumbnailUrl = request.ThumbnailImageId.HasValue
            ? project.Images.Single(image => image.Id == request.ThumbnailImageId).ImageUrl
            : null;
        project.UpdatedAt = timeProvider.GetUtcNow();
        ReplaceTranslations(project, request.Translations);
        ReplaceHighlights(project, request.Highlights);
        ReplaceTechnologies(project, request.TechnologyIds);
        if (project.IsPublished) ValidatePublishing(project);

        if (!id.HasValue) await repository.AddAsync(project, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        foreach (var objectKey in removedKeys)
            await storage.DeleteIfExistsAsync(imageSettings.Bucket, objectKey, cancellationToken);
        LogSaved(logger, project.Id, id.HasValue ? "updated" : "created");
        return MapAdmin(project);
    }

    private static void Validate(ProjectWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Slug) || SlugNormalizer.Normalize(request.Slug).Length > 220)
            throw Invalid("slug", "Slug is required and must not exceed 220 characters after normalization.");
        if (string.IsNullOrWhiteSpace(request.InternalName) || request.InternalName.Trim().Length > 200)
            throw Invalid("internalName", "Internal name is required and must not exceed 200 characters.");
        ValidateHttps(request.RepositoryUrl, "repositoryUrl");
        ValidateHttps(request.DemoUrl, "demoUrl");
        if (request.EndDate.HasValue && request.StartDate.HasValue && request.EndDate < request.StartDate)
            throw Invalid("endDate", "End date must be on or after start date.");
        if (request.DisplayOrder < 0)
            throw Invalid("displayOrder", "Display order must be non-negative.");
        ArgumentNullException.ThrowIfNull(request.Translations);
        ArgumentNullException.ThrowIfNull(request.TechnologyIds);
        ArgumentNullException.ThrowIfNull(request.Highlights);
        ArgumentNullException.ThrowIfNull(request.Images);
        ValidateTranslations(request.Translations, request.IsPublished);
        ValidateHighlights(request.Highlights);
        if (request.TechnologyIds.Distinct().Count() != request.TechnologyIds.Count)
            throw Invalid("technologyIds", "Technology IDs must be unique.");
    }

    private static void ValidateTranslations(
        IReadOnlyDictionary<string, ProjectTranslationRequest> translations,
        bool publishing)
    {
        if (translations.Keys.Any(locale => !SupportedLocales.All.Contains(locale)))
            throw Invalid("translations", "Only 'en' and 'vi' translations are supported.");
        foreach (var pair in translations)
        {
            if (pair.Value is null || string.IsNullOrWhiteSpace(pair.Value.Name) || pair.Value.Name.Trim().Length > 200)
                throw Invalid($"translations.{pair.Key}.name", "Name is required and must not exceed 200 characters.");
            EnsureMax(pair.Value.ShortDescription, 500, $"translations.{pair.Key}.shortDescription");
            EnsureMax(pair.Value.Role, 200, $"translations.{pair.Key}.role");
        }
        if (publishing) PublishTranslationValidation.EnsureComplete(translations.Keys);
    }

    private static void ValidateHighlights(IReadOnlyList<ProjectHighlightRequest> highlights)
    {
        foreach (var item in highlights)
        {
            if (!SupportedLocales.All.Contains(item.Locale))
                throw Invalid("highlights.locale", "Highlight locale must be 'en' or 'vi'.");
            if (string.IsNullOrWhiteSpace(item.Content))
                throw Invalid("highlights.content", "Highlight content is required.");
            if (item.DisplayOrder < 0)
                throw Invalid("highlights.displayOrder", "Highlight display order must be non-negative.");
        }
        if (highlights.GroupBy(item => (item.Locale, item.DisplayOrder)).Any(group => group.Count() > 1))
            throw Invalid("highlights.displayOrder", "Highlight display order must be unique within each locale.");
    }

    private static void ValidateGalleryContract(
        IReadOnlyList<ProjectGalleryUpload> files,
        IReadOnlyList<ProjectGalleryMetadata> metadata,
        int maximumFiles)
    {
        if (files.Count == 0) throw Invalid("files", "At least one gallery file is required.");
        if (files.Count > maximumFiles) throw Invalid("files", "Too many gallery files were supplied.");
        if (metadata.Count != files.Count)
            throw Invalid("metadata", "Each uploaded file requires exactly one metadata item.");
        if (files.Select(item => item.FileIndex).Distinct().Count() != files.Count ||
            metadata.Select(item => item.FileIndex).Distinct().Count() != metadata.Count)
            throw Invalid("fileIndex", "File indexes must be unique.");
        if (!files.Select(item => item.FileIndex).Order().SequenceEqual(Enumerable.Range(0, files.Count)) ||
            !metadata.Select(item => item.FileIndex).Order().SequenceEqual(Enumerable.Range(0, files.Count)))
            throw Invalid("fileIndex", "File indexes must be zero-based and reference every uploaded file exactly once.");
        if (metadata.Any(item => item.DisplayOrder < 0) ||
            metadata.Select(item => item.DisplayOrder).Distinct().Count() != metadata.Count)
            throw Invalid("displayOrder", "Gallery display orders must be non-negative and unique.");
        foreach (var item in metadata) ValidateAltText(item.AltText, true);
    }

    private static void ValidateImageMetadata(
        Project project,
        IReadOnlyList<ProjectImageMetadataRequest> metadata,
        Guid? thumbnailImageId)
    {
        if (metadata.Select(item => item.Id).Distinct().Count() != metadata.Count)
            throw Invalid("images.id", "Image IDs must be unique.");
        if (metadata.Any(item => item.DisplayOrder < 0) ||
            metadata.Select(item => item.DisplayOrder).Distinct().Count() != metadata.Count)
            throw Invalid("images.displayOrder", "Image display orders must be non-negative and unique.");
        var existingIds = project.Images.Select(item => item.Id).ToHashSet();
        if (metadata.Any(item => !existingIds.Contains(item.Id)))
            throw Invalid("images.id", "Every image must belong to this project.");
        if (thumbnailImageId.HasValue && metadata.All(item => item.Id != thumbnailImageId.Value))
            throw Invalid("thumbnailImageId", "Thumbnail image must belong to this project and remain in its gallery.");
        foreach (var item in metadata) ValidateAltText(item.AltText, false);
    }

    private static void ValidateAltText(IReadOnlyDictionary<string, string> altText, bool requireComplete)
    {
        if (altText is null || altText.Keys.Any(locale => !SupportedLocales.All.Contains(locale)))
            throw Invalid("altText", "Only 'en' and 'vi' alt text is supported.");
        if (requireComplete) PublishTranslationValidation.EnsureComplete(altText.Keys);
        foreach (var pair in altText)
            if (string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Trim().Length > 500)
                throw Invalid($"altText.{pair.Key}", "Alt text is required and must not exceed 500 characters.");
    }

    private static void ValidatePublishing(Project project)
    {
        PublishTranslationValidation.EnsureComplete(project.Translations.Select(item => item.LocaleCode));
        if (project.Translations.Any(item => string.IsNullOrWhiteSpace(item.Name)))
            throw Invalid("translations.name", "Publishing requires every project name.");
        foreach (var image in project.Images)
        {
            PublishTranslationValidation.EnsureComplete(image.Translations.Select(item => item.LocaleCode));
            if (image.Translations.Any(item => string.IsNullOrWhiteSpace(item.AltText)))
                throw Invalid("images.altText", "Publishing requires both alt texts for every image.");
        }
    }

    private static void ReplaceTranslations(Project project, IReadOnlyDictionary<string, ProjectTranslationRequest> translations)
    {
        foreach (var obsolete in project.Translations.Where(item => !translations.ContainsKey(item.LocaleCode)).ToArray())
            project.Translations.Remove(obsolete);
        foreach (var pair in translations)
        {
            var target = project.Translations.SingleOrDefault(item => item.LocaleCode == pair.Key);
            if (target is null)
            {
                target = new ProjectTranslation { ProjectId = project.Id, LocaleCode = pair.Key };
                project.Translations.Add(target);
            }
            target.Name = pair.Value.Name.Trim();
            target.ShortDescription = NullIfWhiteSpace(pair.Value.ShortDescription);
            target.ClientContext = NullIfWhiteSpace(pair.Value.ClientContext);
            target.Role = NullIfWhiteSpace(pair.Value.Role);
            target.Problem = NullIfWhiteSpace(pair.Value.Problem);
            target.Solution = NullIfWhiteSpace(pair.Value.Solution);
            target.Result = NullIfWhiteSpace(pair.Value.Result);
        }
    }

    private static void ReplaceHighlights(Project project, IReadOnlyList<ProjectHighlightRequest> highlights)
    {
        var keys = highlights.Select(item => (item.Locale, item.DisplayOrder)).ToHashSet();
        foreach (var obsolete in project.Highlights.Where(item => !keys.Contains((item.LocaleCode, item.DisplayOrder))).ToArray())
            project.Highlights.Remove(obsolete);
        foreach (var item in highlights)
        {
            var target = project.Highlights.SingleOrDefault(existing =>
                existing.LocaleCode == item.Locale && existing.DisplayOrder == item.DisplayOrder);
            if (target is null)
            {
                target = new ProjectHighlight
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    LocaleCode = item.Locale,
                    DisplayOrder = item.DisplayOrder,
                };
                project.Highlights.Add(target);
            }
            target.Content = item.Content.Trim();
        }
    }

    private static void ReplaceTechnologies(Project project, IReadOnlyList<Guid> technologyIds)
    {
        var ids = technologyIds.ToHashSet();
        foreach (var obsolete in project.Technologies.Where(item => !ids.Contains(item.TechnologyId)).ToArray())
            project.Technologies.Remove(obsolete);
        for (var index = 0; index < technologyIds.Count; index++)
        {
            var target = project.Technologies.SingleOrDefault(item => item.TechnologyId == technologyIds[index]);
            if (target is null)
            {
                target = new ProjectTechnology { ProjectId = project.Id, TechnologyId = technologyIds[index] };
                project.Technologies.Add(target);
            }
            target.DisplayOrder = index;
        }
    }

    private static string[] ReplaceImageMetadata(
        Project project, IReadOnlyList<ProjectImageMetadataRequest> metadata)
    {
        var ids = metadata.Select(item => item.Id).ToHashSet();
        var removed = project.Images.Where(item => !ids.Contains(item.Id)).ToArray();
        foreach (var image in removed) project.Images.Remove(image);
        foreach (var item in metadata)
        {
            var image = project.Images.Single(existing => existing.Id == item.Id);
            image.DisplayOrder = item.DisplayOrder;
            image.Translations.Clear();
            foreach (var alt in item.AltText)
                image.Translations.Add(new ProjectImageTranslation
                {
                    ProjectImageId = image.Id,
                    LocaleCode = alt.Key,
                    AltText = alt.Value.Trim(),
                });
        }
        return removed.Select(item => item.ImageUrl).ToArray();
    }

    private PublicProjectListItem MapList(ProjectPublicProjection project) => new(
        project.Id, project.Slug, project.Name, project.ShortDescription,
        project.Kind, project.DisclosureLevel, ResolveUrl(project.ThumbnailObjectKey),
        project.StartDate, project.EndDate, project.IsFeatured, project.Role, project.Technologies);

    private PublicProjectDetail MapDetail(ProjectPublicProjection project)
    {
        var full = project.DisclosureLevel == ProjectDisclosureLevel.Full;
        return new PublicProjectDetail(
            project.Id, project.Slug, project.Name, project.ShortDescription,
            project.Kind, project.DisclosureLevel, ResolveUrl(project.ThumbnailObjectKey),
            project.StartDate, project.EndDate, project.IsFeatured, project.Role,
            project.Technologies, project.Highlights,
            full ? project.RepositoryUrl : null,
            full ? project.DemoUrl : null,
            full ? project.ClientContext : null,
            full ? project.Problem : null,
            full ? project.Solution : null,
            full ? project.Result : null,
            full ? project.Images.Select(image => new ProjectImageResponse(
                image.Id, ResolveUrl(image.ObjectKey)!, image.AltText, image.DisplayOrder)).ToArray() : null);
    }

    private ProjectAdminResponse MapAdmin(Project project)
    {
        var thumbnail = project.Images.SingleOrDefault(image => image.ImageUrl == project.ThumbnailUrl);
        return new ProjectAdminResponse(
            project.Id, project.Slug, project.InternalName, project.Kind, project.DisclosureLevel,
            ResolveUrl(project.ThumbnailUrl), project.RepositoryUrl, project.DemoUrl, thumbnail?.Id,
            project.StartDate, project.EndDate, project.IsFeatured, project.DisplayOrder, project.IsPublished,
            project.Translations.ToDictionary(item => item.LocaleCode, item => new ProjectTranslationRequest(
                item.Name, item.ShortDescription, item.ClientContext, item.Role,
                item.Problem, item.Solution, item.Result), StringComparer.Ordinal),
            project.Technologies.OrderBy(item => item.DisplayOrder).Select(item => item.TechnologyId).ToArray(),
            project.Highlights.OrderBy(item => item.LocaleCode).ThenBy(item => item.DisplayOrder)
                .Select(item => new ProjectHighlightRequest(item.LocaleCode, item.Content, item.DisplayOrder)).ToArray(),
            project.Images.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).Select(MapImageAdmin).ToArray());
    }

    private ProjectImageMetadataResponse MapImageAdmin(ProjectImage image) => new(
        image.Id, ResolveUrl(image.ImageUrl)!, image.DisplayOrder,
        image.Translations.ToDictionary(item => item.LocaleCode, item => item.AltText, StringComparer.Ordinal));

    private string? ResolveUrl(string? objectKey) => string.IsNullOrWhiteSpace(objectKey)
        ? null : storage.GetPublicReadUrl(imageSettings.Bucket, objectKey).AbsoluteUri;

    private async Task CompensateAsync(IEnumerable<StorageObject> objects)
    {
        foreach (var item in objects)
        {
            try
            {
                await storage.DeleteIfExistsAsync(item.Bucket, item.ObjectKey, CancellationToken.None);
            }
            catch (Exception exception)
            {
                LogCompensationFailed(logger, item.Bucket, item.ObjectKey, exception);
            }
        }
    }

    private static void ValidateHttps(string? value, string field)
    {
        if (value is not null && (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw Invalid(field, "URL must be an absolute HTTPS URL.");
    }

    private static void EnsureMax(string? value, int maximum, string field)
    {
        if (value?.Trim().Length > maximum)
            throw Invalid(field, $"Value must not exceed {maximum} characters.");
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ValidationException Invalid(string field, string message) =>
        new("Project validation failed.", new Dictionary<string, string[]> { [field] = [message] });

    [LoggerMessage(EventId = 3401, Level = LogLevel.Information, Message = "Project {ProjectId} {Operation}.")]
    private static partial void LogSaved(ILogger logger, Guid projectId, string operation);
    [LoggerMessage(EventId = 3402, Level = LogLevel.Information, Message = "Project {ProjectId} deleted.")]
    private static partial void LogDeleted(ILogger logger, Guid projectId);
    [LoggerMessage(EventId = 3403, Level = LogLevel.Information, Message = "Project {ProjectId} publication set to {IsPublished}.")]
    private static partial void LogPublished(ILogger logger, Guid projectId, bool isPublished);
    [LoggerMessage(EventId = 3404, Level = LogLevel.Information, Message = "Reordered {Count} projects.")]
    private static partial void LogReordered(ILogger logger, int count);
    [LoggerMessage(EventId = 3405, Level = LogLevel.Information, Message = "Uploaded {Count} gallery images for project {ProjectId}.")]
    private static partial void LogGalleryUploaded(ILogger logger, Guid projectId, int count);
    [LoggerMessage(EventId = 3406, Level = LogLevel.Error, Message = "Gallery compensation failed for bucket {Bucket} and object {ObjectKey}.")]
    private static partial void LogCompensationFailed(ILogger logger, string bucket, string objectKey, Exception exception);
}
