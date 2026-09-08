using Portfolio.Application.Certificates;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;

namespace Portfolio.Infrastructure.Availability;

public sealed class DatabaseUnavailableCertificateRepository : ICertificateRepository
{
    private static ServiceUnavailableException Unavailable() => new("PostgreSQL is not configured.");

    public Task<IReadOnlyList<CertificatePublicProjection>?> GetPublicAsync(string slug, string locale, CancellationToken cancellationToken) => throw Unavailable();
    public Task<CertificateEntityPage> GetCertificatesAsync(CertificateAdminQuery query, CancellationToken cancellationToken) => throw Unavailable();
    public Task<Certificate?> GetAsync(Guid id, bool tracked, CancellationToken cancellationToken) => throw Unavailable();
    public Task<bool> TechnologyIdsExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => throw Unavailable();
    public Task AddAsync(Certificate certificate, CancellationToken cancellationToken) => throw Unavailable();
    public void Remove(Certificate certificate) => throw Unavailable();
    public Task SaveChangesAsync(CancellationToken cancellationToken) => throw Unavailable();
}
