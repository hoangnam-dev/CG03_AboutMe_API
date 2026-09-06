using Portfolio.Application.Common.Models;

namespace Portfolio.Application.About;

public interface IAboutRepository
{
    Task<AboutPublicProjection?> GetPublicAsync(string slug, string locale, CancellationToken cancellationToken);
    Task<Common.Models.About?> GetAdminAsync(CancellationToken cancellationToken);
    Task<Common.Models.About?> GetForUpdateAsync(CancellationToken cancellationToken);
    Task<Guid?> GetProfileIdAsync(CancellationToken cancellationToken);
    Task AddAsync(Common.Models.About about, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
