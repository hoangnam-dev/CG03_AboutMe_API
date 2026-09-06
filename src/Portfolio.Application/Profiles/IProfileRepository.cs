using Portfolio.Application.Common.Models;

namespace Portfolio.Application.Profiles;

public interface IProfileRepository
{
    Task<ProfilePublicProjection?> GetPublicAsync(string slug, string locale, CancellationToken cancellationToken);
    Task<Profile?> GetAdminAsync(CancellationToken cancellationToken);
    Task<Profile?> GetForUpdateAsync(CancellationToken cancellationToken);
    Task<bool> SlugExistsAsync(string slug, Guid? excludedId, CancellationToken cancellationToken);
    Task AddAsync(Profile profile, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
