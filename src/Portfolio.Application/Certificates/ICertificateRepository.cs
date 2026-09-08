using Portfolio.Application.Common.Models;

namespace Portfolio.Application.Certificates;

public interface ICertificateRepository
{
    Task<IReadOnlyList<CertificatePublicProjection>?> GetPublicAsync(string slug, string locale, CancellationToken cancellationToken);
    Task<CertificateEntityPage> GetCertificatesAsync(CertificateAdminQuery query, CancellationToken cancellationToken);
    Task<Certificate?> GetAsync(Guid id, bool tracked, CancellationToken cancellationToken);
    Task<bool> TechnologyIdsExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task AddAsync(Certificate certificate, CancellationToken cancellationToken);
    void Remove(Certificate certificate);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
