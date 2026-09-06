using System.Text.Json.Serialization;
using Portfolio.Application.Common.Models;

namespace Portfolio.Application.Profiles;

public sealed record ProfileTranslationRequest(
    string Title,
    string? ShortBio,
    string? Location,
    string? Availability);

public sealed record SocialLinkRequest(
    string Platform,
    string? Label,
    string Url,
    string? IconName,
    int DisplayOrder,
    bool IsPublished);

public sealed record ProfileUpdateRequest(
    string Slug,
    string FullName,
    string? Email,
    string? Phone,
    bool ShowEmail,
    bool ShowPhone,
    bool AvailableForWork,
    IReadOnlyDictionary<string, ProfileTranslationRequest> Translations,
    IReadOnlyList<SocialLinkRequest> SocialLinks);

public sealed record PublicSocialLink(
    string Platform,
    string? Label,
    string Url,
    string? IconName,
    int DisplayOrder);

public sealed record ProfilePublicProjection(
    string Slug,
    string FullName,
    string Title,
    string? ShortBio,
    string? Location,
    string? Availability,
    bool AvailableForWork,
    string? Email,
    string? Phone,
    bool ShowEmail,
    bool ShowPhone,
    string? AvatarObjectKey,
    string? HeroImageObjectKey,
    IReadOnlyList<PublicSocialLink> SocialLinks);

public sealed record PublicProfileResponse(
    string Slug,
    string FullName,
    string Title,
    string? ShortBio,
    string? Location,
    string? Availability,
    bool AvailableForWork,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Email,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Phone,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AvatarUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? HeroImageUrl,
    IReadOnlyList<PublicSocialLink> SocialLinks);

public sealed record ProfileAdminResponse(
    Guid Id,
    string Slug,
    string FullName,
    string? Email,
    string? Phone,
    bool ShowEmail,
    bool ShowPhone,
    bool AvailableForWork,
    string? AvatarUrl,
    string? HeroImageUrl,
    IReadOnlyDictionary<string, ProfileTranslationRequest> Translations,
    IReadOnlyList<SocialLinkRequest> SocialLinks);

public sealed record ProfileMediaUpload(
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long Length);

public sealed record ProfileMediaResponse(string Url);

public sealed record AvatarUploadResponse(string AvatarUrl);

public sealed record HeroImageUploadResponse(string HeroImageUrl);

public sealed record ProfileMediaSettings(
    string Bucket,
    long MaxFileSize,
    int MaxHeroImageWidth = 8192,
    int MaxHeroImageHeight = 8192);
