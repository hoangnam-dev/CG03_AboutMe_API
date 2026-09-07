namespace Portfolio.Application.Profiles;

public interface IProfileService
{
    Task<PublicProfileResponse> GetPublicAsync(string slug, string? locale, CancellationToken cancellationToken);
    Task<ProfileAdminResponse> GetAdminAsync(CancellationToken cancellationToken);
    Task<ProfileAdminResponse> UpdateAsync(ProfileUpdateRequest request, CancellationToken cancellationToken);
    Task<ProfileMediaResponse> ReplaceAvatarAsync(ProfileMediaUpload upload, CancellationToken cancellationToken);
    Task<ProfileMediaResponse> ReplaceHeroImageAsync(ProfileMediaUpload upload, CancellationToken cancellationToken);
}
