using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Localization;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Common.Storage;
using Portfolio.Application.Common.Validation;

namespace Portfolio.Application.Profiles;

public sealed partial class ProfileService(
    IProfileRepository repository,
    IFileStorage storage,
    StorageReplacement storageReplacement,
    ProfileMediaSettings mediaSettings,
    TimeProvider timeProvider,
    ILogger<ProfileService> logger) : IProfileService
{
    public async Task<PublicProfileResponse> GetPublicAsync(
        string slug,
        string? locale,
        CancellationToken cancellationToken)
    {
        var normalizedSlug = SlugNormalizer.Normalize(slug);
        var normalizedLocale = SupportedLocales.Normalize(locale);
        var profile = await repository.GetPublicAsync(
            normalizedSlug, normalizedLocale, cancellationToken)
            ?? throw new NotFoundException("Profile was not found.");

        return new PublicProfileResponse(
            profile.Slug,
            profile.FullName,
            profile.Title,
            profile.ShortBio,
            profile.Location,
            profile.Availability,
            profile.AvailableForWork,
            profile.ShowEmail ? profile.Email : null,
            profile.ShowPhone ? profile.Phone : null,
            ResolvePublicUrl(profile.AvatarObjectKey),
            ResolvePublicUrl(profile.HeroImageObjectKey),
            profile.SocialLinks);
    }

    public async Task<ProfileAdminResponse> GetAdminAsync(CancellationToken cancellationToken) =>
        MapAdmin(await repository.GetAdminAsync(cancellationToken)
            ?? throw new NotFoundException("Profile was not found."));

    public async Task<ProfileAdminResponse> UpdateAsync(
        ProfileUpdateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        var slug = SlugNormalizer.Normalize(request.Slug);
        if (slug.Length > 220)
        {
            throw Invalid("slug", "Slug must not exceed 220 characters.");
        }

        var profile = await repository.GetForUpdateAsync(cancellationToken);
        if (await repository.SlugExistsAsync(slug, profile?.Id, cancellationToken))
        {
            throw new ConflictException("The normalized profile slug already exists.");
        }

        var now = timeProvider.GetUtcNow();
        if (profile is null)
        {
            profile = new Profile { Id = Guid.NewGuid(), CreatedAt = now };
            await repository.AddAsync(profile, cancellationToken);
        }

        profile.Slug = slug;
        profile.FullName = request.FullName.Trim();
        profile.Email = NullIfWhiteSpace(request.Email);
        profile.Phone = NullIfWhiteSpace(request.Phone);
        profile.ShowEmail = request.ShowEmail;
        profile.ShowPhone = request.ShowPhone;
        profile.AvailableForWork = request.AvailableForWork;
        profile.UpdatedAt = now;
        foreach (var obsolete in profile.Translations
                     .Where(current => !request.Translations.ContainsKey(current.LocaleCode))
                     .ToArray())
            profile.Translations.Remove(obsolete);
        foreach (var translation in request.Translations.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var target = profile.Translations.SingleOrDefault(
                current => current.LocaleCode == translation.Key);
            if (target is null)
            {
                target = new ProfileTranslation
                {
                    ProfileId = profile.Id,
                    LocaleCode = translation.Key,
                };
                profile.Translations.Add(target);
            }
            target.Title = translation.Value.Title.Trim();
            target.ShortBio = NullIfWhiteSpace(translation.Value.ShortBio);
            target.Location = NullIfWhiteSpace(translation.Value.Location);
            target.Availability = NullIfWhiteSpace(translation.Value.Availability);
            target.UpdatedAt = now;
        }

        var requestedPlatforms = request.SocialLinks
            .Select(link => link.Platform.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var obsolete in profile.SocialLinks
                     .Where(current => !requestedPlatforms.Contains(current.Platform))
                     .ToArray())
            profile.SocialLinks.Remove(obsolete);
        foreach (var link in request.SocialLinks)
        {
            var platform = link.Platform.Trim().ToLowerInvariant();
            var target = profile.SocialLinks.SingleOrDefault(
                current => string.Equals(current.Platform, platform, StringComparison.OrdinalIgnoreCase));
            if (target is null)
            {
                target = new SocialLink { Id = Guid.NewGuid(), ProfileId = profile.Id };
                profile.SocialLinks.Add(target);
            }
            target.Platform = platform;
            target.Label = NullIfWhiteSpace(link.Label);
            target.Url = link.Url;
            target.IconName = NullIfWhiteSpace(link.IconName);
            target.DisplayOrder = link.DisplayOrder;
            target.IsPublished = link.IsPublished;
        }

        await repository.SaveChangesAsync(cancellationToken);
        LogProfileUpdated(logger, profile.Id);
        return MapAdmin(profile);
    }

    public Task<ProfileMediaResponse> ReplaceAvatarAsync(
        ProfileMediaUpload upload,
        CancellationToken cancellationToken) =>
        ReplaceMediaAsync(upload, false, cancellationToken);

    public Task<ProfileMediaResponse> ReplaceHeroImageAsync(
        ProfileMediaUpload upload,
        CancellationToken cancellationToken) =>
        ReplaceMediaAsync(upload, true, cancellationToken);

    private async Task<ProfileMediaResponse> ReplaceMediaAsync(
        ProfileMediaUpload upload,
        bool hero,
        CancellationToken cancellationToken)
    {
        var profile = await repository.GetForUpdateAsync(cancellationToken)
            ?? throw new NotFoundException("Profile was not found.");
        var validated = await FileValidation.ValidateAsync(
            new FileUpload(upload.Content, upload.OriginalFileName, upload.ContentType, upload.Length),
            FileValidationOptions.Images(mediaSettings.MaxFileSize),
            cancellationToken);
        if (hero)
        {
            await ImageDimensionValidation.EnsureWithinAsync(
                upload.Content,
                validated.Kind,
                mediaSettings.MaxHeroImageWidth,
                mediaSettings.MaxHeroImageHeight,
                cancellationToken);
        }
        var objectKey = $"profiles/{profile.Id:D}/{Guid.NewGuid():N}{validated.Extension}";
        var oldObjectKey = hero ? profile.HeroImageUrl : profile.AvatarUrl;
        var storageUpload = new StorageUpload(
            mediaSettings.Bucket, objectKey, upload.Content, validated.ContentType, validated.Length);

        var stored = await storageReplacement.ReplaceAsync(
            storageUpload,
            oldObjectKey,
            async (newObject, token) =>
            {
                if (hero) profile.HeroImageUrl = newObject.ObjectKey;
                else profile.AvatarUrl = newObject.ObjectKey;
                profile.UpdatedAt = timeProvider.GetUtcNow();
                await repository.SaveChangesAsync(token);
            },
            cancellationToken);

        LogMediaReplaced(logger, profile.Id, hero ? "hero-image" : "avatar");
        return new ProfileMediaResponse(storage.GetPublicReadUrl(stored.Bucket, stored.ObjectKey).AbsoluteUri);
    }

    private ProfileAdminResponse MapAdmin(Profile profile) => new(
        profile.Id,
        profile.Slug,
        profile.FullName,
        profile.Email,
        profile.Phone,
        profile.ShowEmail,
        profile.ShowPhone,
        profile.AvailableForWork,
        ResolvePublicUrl(profile.AvatarUrl),
        ResolvePublicUrl(profile.HeroImageUrl),
        profile.Translations.ToDictionary(
            translation => translation.LocaleCode,
            translation => new ProfileTranslationRequest(
                translation.Title, translation.ShortBio, translation.Location, translation.Availability),
            StringComparer.Ordinal),
        profile.SocialLinks.OrderBy(link => link.DisplayOrder).ThenBy(link => link.Id)
            .Select(link => new SocialLinkRequest(
                link.Platform, link.Label, link.Url, link.IconName, link.DisplayOrder, link.IsPublished))
            .ToArray());

    private string? ResolvePublicUrl(string? objectKey) => string.IsNullOrWhiteSpace(objectKey)
        ? null
        : storage.GetPublicReadUrl(mediaSettings.Bucket, objectKey).AbsoluteUri;

    private static void Validate(ProfileUpdateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 150)
            throw Invalid("fullName", "Full name is required and must not exceed 150 characters.");
        if (request.Email is { Length: > 320 } ||
            (!string.IsNullOrWhiteSpace(request.Email) && !MailAddress.TryCreate(request.Email, out _)))
            throw Invalid("email", "Email must be valid and not exceed 320 characters.");
        if (request.Phone?.Length > 30)
            throw Invalid("phone", "Phone must not exceed 30 characters.");
        if (request.ShowEmail && string.IsNullOrWhiteSpace(request.Email))
            throw Invalid("showEmail", "A visible email requires an email value.");
        if (request.ShowPhone && string.IsNullOrWhiteSpace(request.Phone))
            throw Invalid("showPhone", "A visible phone requires a phone value.");

        PublishTranslationValidation.EnsureComplete(request.Translations.Keys);
        if (request.Translations.Keys.Any(locale => !SupportedLocales.All.Contains(locale)))
            throw Invalid("translations", "Only 'en' and 'vi' translations are supported.");
        foreach (var translation in request.Translations.Values)
        {
            if (string.IsNullOrWhiteSpace(translation.Title) || translation.Title.Trim().Length > 200)
                throw Invalid("translations.title", "Title is required and must not exceed 200 characters.");
            EnsureMax(translation.ShortBio, 500, "translations.shortBio");
            EnsureMax(translation.Location, 200, "translations.location");
            EnsureMax(translation.Availability, 250, "translations.availability");
        }

        var platforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orders = new HashSet<int>();
        foreach (var link in request.SocialLinks)
        {
            if (string.IsNullOrWhiteSpace(link.Platform) || link.Platform.Trim().Length > 50)
                throw Invalid("socialLinks.platform", "Platform is required and must not exceed 50 characters.");
            if (!platforms.Add(link.Platform.Trim()))
                throw new ConflictException("Social-link platform values must be unique.");
            if (link.DisplayOrder < 0 || !orders.Add(link.DisplayOrder))
                throw new ConflictException("Social-link display orders must be non-negative and unique.");
            if (!Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw Invalid("socialLinks.url", "Social-link URLs must be absolute HTTPS URLs.");
            EnsureMax(link.Label, 100, "socialLinks.label");
            EnsureMax(link.IconName, 100, "socialLinks.iconName");
        }
    }

    private static void EnsureMax(string? value, int maximum, string field)
    {
        if (value?.Length > maximum) throw Invalid(field, $"Value must not exceed {maximum} characters.");
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ValidationException Invalid(string field, string message) =>
        new("Profile validation failed.", new Dictionary<string, string[]> { [field] = [message] });

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Profile {ProfileId} updated.")]
    private static partial void LogProfileUpdated(ILogger logger, Guid profileId);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Profile {ProfileId} {MediaKind} replaced.")]
    private static partial void LogMediaReplaced(ILogger logger, Guid profileId, string mediaKind);
}
