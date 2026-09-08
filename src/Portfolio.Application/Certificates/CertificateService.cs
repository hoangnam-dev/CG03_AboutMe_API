using Microsoft.Extensions.Logging;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Localization;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Common.Storage;
using Portfolio.Application.Common.Validation;

namespace Portfolio.Application.Certificates;

public sealed partial class CertificateService(
    ICertificateRepository repository,
    IFileStorage storage,
    CertificateEvidenceSettings evidenceSettings,
    TimeProvider timeProvider,
    ILogger<CertificateService> logger) : ICertificateService
{
    public async Task<IReadOnlyList<CertificatePublicResponse>> GetPublicAsync(
        string slug, string? locale, CancellationToken cancellationToken)
    {
        var items = await repository.GetPublicAsync(
            SlugNormalizer.Normalize(slug), SupportedLocales.Normalize(locale), cancellationToken)
            ?? throw new NotFoundException("Portfolio was not found.");
        var responses = new List<CertificatePublicResponse>(items.Count);
        foreach (var item in items)
        {
            var fileAccess = await CreateSignedAccessAsync(item.FileObjectKey, cancellationToken);
            var imageAccess = await CreateSignedAccessAsync(item.ImageObjectKey, cancellationToken);
            responses.Add(new CertificatePublicResponse(
                item.Id,
                item.Issuer,
                item.IssuedDate,
                item.ExpirationDate,
                IsExpired(item.ExpirationDate),
                item.ShowCredentialId ? item.CredentialId : null,
                item.CredentialUrl,
                item.DisplayOrder,
                item.Name,
                item.Technologies,
                fileAccess?.Url,
                fileAccess?.ExpiresAt,
                imageAccess?.Url,
                imageAccess?.ExpiresAt));
        }
        return responses;
    }

    public async Task<CertificateAdminPage> GetCertificatesAsync(
        CertificateAdminQuery query, CancellationToken cancellationToken)
    {
        if (query.Page < 1) throw Invalid("page", "Page must be at least 1.");
        if (query.PageSize is < 1 or > 100)
            throw Invalid("pageSize", "Page size must be between 1 and 100.");
        if (query.Search?.Length > 200)
            throw Invalid("search", "Search must not exceed 200 characters.");
        var page = await repository.GetCertificatesAsync(
            query with { Search = NullIfWhiteSpace(query.Search) }, cancellationToken);
        var items = new List<CertificateAdminResponse>(page.Items.Count);
        foreach (var certificate in page.Items)
            items.Add(await MapAdminAsync(certificate, cancellationToken));
        return new CertificateAdminPage(items, page.Total, page.Page, page.PageSize);
    }

    public async Task<CertificateAdminResponse> GetCertificateAsync(
        Guid id, CancellationToken cancellationToken) =>
        await MapAdminAsync(
            await repository.GetAsync(id, false, cancellationToken)
                ?? throw new NotFoundException("Certificate was not found."),
            cancellationToken);

    public Task<CertificateAdminResponse> CreateCertificateAsync(
        CertificateWriteRequest request, CancellationToken cancellationToken) =>
        SaveAsync(null, request, cancellationToken);

    public Task<CertificateAdminResponse> UpdateCertificateAsync(
        Guid id, CertificateWriteRequest request, CancellationToken cancellationToken) =>
        SaveAsync(id, request, cancellationToken);

    public async Task DeleteCertificateAsync(Guid id, CancellationToken cancellationToken)
    {
        var certificate = await repository.GetAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("Certificate was not found.");
        var objectKeys = EvidenceKeys(certificate).ToArray();
        repository.Remove(certificate);
        await repository.SaveChangesAsync(cancellationToken);
        foreach (var objectKey in objectKeys)
            await storage.DeleteIfExistsAsync(evidenceSettings.Bucket, objectKey, cancellationToken);
        LogDeleted(logger, id);
    }

    public async Task<CertificateEvidenceResponse> UploadEvidenceAsync(
        Guid id,
        CertificateEvidenceUpload upload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        var certificate = await repository.GetAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("Certificate was not found.");
        var validated = await FileValidation.ValidateAsync(
            new FileUpload(upload.Content, upload.OriginalFileName, upload.ContentType, upload.Length),
            FileValidationOptions.ImagesAndPdf(evidenceSettings.MaxFileSize),
            cancellationToken);
        var objectKey = $"certificates/{certificate.Id}/{Guid.NewGuid():N}{validated.Extension}";
        var stored = await storage.UploadAsync(
            new StorageUpload(
                evidenceSettings.Bucket,
                objectKey,
                upload.Content,
                validated.ContentType,
                validated.Length),
            cancellationToken);
        var oldKey = validated.Kind == FileKind.Pdf
            ? certificate.FileUrl
            : certificate.ImageUrl;
        var oldFileUrl = certificate.FileUrl;
        var oldImageUrl = certificate.ImageUrl;
        try
        {
            if (validated.Kind == FileKind.Pdf)
            {
                certificate.FileUrl = stored.ObjectKey;
            }
            else
            {
                certificate.ImageUrl = stored.ObjectKey;
            }
            certificate.UpdatedAt = timeProvider.GetUtcNow();
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            certificate.FileUrl = oldFileUrl;
            certificate.ImageUrl = oldImageUrl;
            try
            {
                await storage.DeleteIfExistsAsync(
                    stored.Bucket, stored.ObjectKey, CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                LogCompensationFailure(logger, stored.Bucket, stored.ObjectKey, cleanupException);
            }
            throw;
        }

        if (!string.IsNullOrWhiteSpace(oldKey) &&
            !string.Equals(oldKey, stored.ObjectKey, StringComparison.Ordinal))
            await storage.DeleteIfExistsAsync(evidenceSettings.Bucket, oldKey, cancellationToken);

        LogEvidenceUploaded(logger, certificate.Id, validated.Kind);
        return await CreateEvidenceResponseAsync(
            certificate.FileUrl, certificate.ImageUrl, cancellationToken);
    }

    private async Task<CertificateAdminResponse> SaveAsync(
        Guid? id,
        CertificateWriteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        if (!await repository.TechnologyIdsExistAsync(request.TechnologyIds, cancellationToken))
            throw Invalid("technologyIds", "Every technology ID must exist.");

        var certificate = id.HasValue
            ? await repository.GetAsync(id.Value, true, cancellationToken)
                ?? throw new NotFoundException("Certificate was not found.")
            : new Certificate { Id = Guid.NewGuid(), CreatedAt = timeProvider.GetUtcNow() };
        certificate.Issuer = request.Issuer.Trim();
        certificate.IssuedDate = request.IssuedDate;
        certificate.ExpirationDate = request.ExpirationDate;
        certificate.CredentialId = NullIfWhiteSpace(request.CredentialId);
        certificate.ShowCredentialId = request.ShowCredentialId;
        certificate.CredentialUrl = NullIfWhiteSpace(request.CredentialUrl);
        certificate.DisplayOrder = request.DisplayOrder;
        certificate.IsPublished = request.IsPublished;
        certificate.UpdatedAt = timeProvider.GetUtcNow();
        ReplaceTranslations(certificate, request.Translations);
        ReplaceTechnologies(certificate, request.TechnologyIds);
        if (!id.HasValue) await repository.AddAsync(certificate, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        LogSaved(logger, certificate.Id, id.HasValue ? "updated" : "created");
        return await MapAdminAsync(certificate, cancellationToken);
    }

    private static void Validate(CertificateWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Issuer) || request.Issuer.Trim().Length > 200)
            throw Invalid("issuer", "Issuer is required and must not exceed 200 characters.");
        if (request.IssuedDate == default)
            throw Invalid("issuedDate", "Issue date is required.");
        if (request.ExpirationDate < request.IssuedDate)
            throw Invalid("expirationDate", "Expiration date must be on or after issue date.");
        if (request.CredentialId?.Trim().Length > 200)
            throw Invalid("credentialId", "Credential ID must not exceed 200 characters.");
        ValidateHttps(request.CredentialUrl);
        if (request.DisplayOrder < 0)
            throw Invalid("displayOrder", "Display order must be non-negative.");
        if (request.Translations is null)
            throw Invalid("translations", "Translations are required.");
        if (request.TechnologyIds is null)
            throw Invalid("technologyIds", "Technology IDs are required.");
        if (request.Translations.Keys.Any(locale => !SupportedLocales.All.Contains(locale)))
            throw Invalid("translations", "Only 'en' and 'vi' translations are supported.");
        foreach (var pair in request.Translations)
            if (pair.Value is null || string.IsNullOrWhiteSpace(pair.Value.Name) || pair.Value.Name.Trim().Length > 200)
                throw Invalid($"translations.{pair.Key}.name", "Name is required and must not exceed 200 characters.");
        if (request.IsPublished)
            PublishTranslationValidation.EnsureComplete(request.Translations.Keys);
        if (request.TechnologyIds.Distinct().Count() != request.TechnologyIds.Count)
            throw Invalid("technologyIds", "Technology IDs must be unique.");
    }

    private static void ValidateHttps(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            throw Invalid("credentialUrl", "Credential URL must be an absolute HTTPS URL without user information.");
    }

    private static void ReplaceTranslations(
        Certificate certificate,
        IReadOnlyDictionary<string, CertificateTranslationRequest> translations)
    {
        foreach (var existing in certificate.Translations.ToArray())
            if (!translations.ContainsKey(existing.LocaleCode)) certificate.Translations.Remove(existing);
        foreach (var pair in translations)
        {
            var translation = certificate.Translations.SingleOrDefault(item => item.LocaleCode == pair.Key);
            if (translation is null)
            {
                translation = new CertificateTranslation
                {
                    CertificateId = certificate.Id,
                    LocaleCode = pair.Key,
                };
                certificate.Translations.Add(translation);
            }
            translation.Name = pair.Value.Name.Trim();
        }
    }

    private static void ReplaceTechnologies(Certificate certificate, IReadOnlyList<Guid> technologyIds)
    {
        var requested = technologyIds.ToHashSet();
        foreach (var existing in certificate.Technologies.ToArray())
            if (!requested.Contains(existing.TechnologyId)) certificate.Technologies.Remove(existing);
        foreach (var technologyId in technologyIds)
            if (certificate.Technologies.All(item => item.TechnologyId != technologyId))
                certificate.Technologies.Add(new CertificateTechnology
                {
                    CertificateId = certificate.Id,
                    TechnologyId = technologyId,
                });
    }

    private async Task<CertificateAdminResponse> MapAdminAsync(
        Certificate certificate,
        CancellationToken cancellationToken)
    {
        var access = await CreateEvidenceResponseAsync(
            certificate.FileUrl, certificate.ImageUrl, cancellationToken);
        return new CertificateAdminResponse(
            certificate.Id,
            certificate.Issuer,
            certificate.IssuedDate,
            certificate.ExpirationDate,
            IsExpired(certificate.ExpirationDate),
            certificate.CredentialId,
            certificate.ShowCredentialId,
            certificate.CredentialUrl,
            certificate.DisplayOrder,
            certificate.IsPublished,
            certificate.Translations.ToDictionary(
                item => item.LocaleCode,
                item => new CertificateTranslationRequest(item.Name),
                StringComparer.Ordinal),
            certificate.Technologies.Select(item => item.TechnologyId).Order().ToArray(),
            access.DownloadUrl,
            access.DownloadUrlExpiresAt,
            access.ImageUrl,
            access.ImageUrlExpiresAt);
    }

    private async Task<CertificateEvidenceResponse> CreateEvidenceResponseAsync(
        string? fileObjectKey,
        string? imageObjectKey,
        CancellationToken cancellationToken)
    {
        var file = await CreateSignedAccessAsync(fileObjectKey, cancellationToken);
        var image = await CreateSignedAccessAsync(imageObjectKey, cancellationToken);
        return new CertificateEvidenceResponse(
            file?.Url,
            file?.ExpiresAt,
            image?.Url,
            image?.ExpiresAt);
    }

    private async Task<SignedAccess?> CreateSignedAccessAsync(
        string? objectKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectKey)) return null;
        var url = await storage.CreateSignedReadUrlAsync(
            evidenceSettings.Bucket, objectKey, evidenceSettings.SignedUrlLifetime, cancellationToken);
        return new SignedAccess(
            url,
            timeProvider.GetUtcNow().Add(evidenceSettings.SignedUrlLifetime));
    }

    private bool IsExpired(DateOnly? expirationDate) =>
        expirationDate.HasValue && expirationDate.Value < DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private static IEnumerable<string> EvidenceKeys(Certificate certificate)
    {
        if (!string.IsNullOrWhiteSpace(certificate.FileUrl)) yield return certificate.FileUrl;
        if (!string.IsNullOrWhiteSpace(certificate.ImageUrl) &&
            !string.Equals(certificate.ImageUrl, certificate.FileUrl, StringComparison.Ordinal))
            yield return certificate.ImageUrl;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ValidationException Invalid(string field, string message) =>
        new("Certificate validation failed.", new Dictionary<string, string[]> { [field] = [message] });

    private sealed record SignedAccess(Uri Url, DateTimeOffset ExpiresAt);

    [LoggerMessage(EventId = 2601, Level = LogLevel.Information, Message = "Certificate {CertificateId} was {Operation}.")]
    private static partial void LogSaved(ILogger logger, Guid certificateId, string operation);

    [LoggerMessage(EventId = 2602, Level = LogLevel.Information, Message = "Certificate {CertificateId} was deleted.")]
    private static partial void LogDeleted(ILogger logger, Guid certificateId);

    [LoggerMessage(EventId = 2603, Level = LogLevel.Information, Message = "Certificate {CertificateId} evidence was replaced with {FileKind}.")]
    private static partial void LogEvidenceUploaded(ILogger logger, Guid certificateId, FileKind fileKind);

    [LoggerMessage(EventId = 2604, Level = LogLevel.Error, Message = "Storage compensation failed for bucket {Bucket} and object {ObjectKey}; reconciliation is required.")]
    private static partial void LogCompensationFailure(ILogger logger, string bucket, string objectKey, Exception exception);
}
