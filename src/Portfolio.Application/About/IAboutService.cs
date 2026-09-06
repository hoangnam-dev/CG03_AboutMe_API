namespace Portfolio.Application.About;

public interface IAboutService
{
    Task<PublicAboutResponse> GetPublicAsync(string slug, string? locale, CancellationToken cancellationToken);
    Task<AboutAdminResponse> GetAdminAsync(CancellationToken cancellationToken);
    Task<AboutAdminResponse> UpdateAsync(AboutUpdateRequest request, CancellationToken cancellationToken);
}
