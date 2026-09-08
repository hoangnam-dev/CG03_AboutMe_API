using Microsoft.EntityFrameworkCore;
using Portfolio.Application.Certificates;
using Portfolio.Application.Common.Models;

namespace Portfolio.Infrastructure.Persistence.Repositories;

public sealed class CertificateRepository(PortfolioDbContext context) : ICertificateRepository
{
    public async Task<IReadOnlyList<CertificatePublicProjection>?> GetPublicAsync(
        string slug, string locale, CancellationToken cancellationToken)
    {
        if (!await context.Profiles.AsNoTracking()
            .AnyAsync(profile => profile.Slug == slug, cancellationToken))
            return null;

        var certificates = await context.Certificates.AsNoTracking()
            .Where(certificate => certificate.IsPublished &&
                certificate.Translations.Any(item => item.LocaleCode == locale))
            .OrderBy(certificate => certificate.DisplayOrder)
            .ThenByDescending(certificate => certificate.IssuedDate)
            .ThenBy(certificate => certificate.Id)
            .Include(certificate => certificate.Translations.Where(item => item.LocaleCode == locale))
            .Include(certificate => certificate.Technologies.Where(link =>
                link.Technology.IsPublished &&
                link.Technology.Category.IsPublished &&
                link.Technology.Translations.Any(item => item.LocaleCode == locale)))
                .ThenInclude(link => link.Technology)
                    .ThenInclude(technology => technology.Translations.Where(item => item.LocaleCode == locale))
            .AsSplitQuery()
            .ToArrayAsync(cancellationToken);

        return certificates.Select(certificate => new CertificatePublicProjection(
            certificate.Id,
            certificate.Issuer,
            certificate.IssuedDate,
            certificate.ExpirationDate,
            certificate.CredentialId,
            certificate.ShowCredentialId,
            certificate.CredentialUrl,
            certificate.DisplayOrder,
            certificate.Translations.Single().Name,
            certificate.FileUrl,
            certificate.ImageUrl,
            certificate.Technologies
                .Where(link => link.Technology.Translations.Count != 0)
                .OrderBy(link => link.Technology.DisplayOrder)
                .ThenBy(link => link.TechnologyId)
                .Select(link => new CertificateTechnologyResponse(
                    link.TechnologyId,
                    link.Technology.Translations.Single().Name,
                    link.Technology.IconType.ToString().ToLowerInvariant(),
                    link.Technology.IconValue,
                    link.Technology.DisplayOrder))
                .ToArray()))
            .ToArray();
    }

    public async Task<CertificateEntityPage> GetCertificatesAsync(
        CertificateAdminQuery query, CancellationToken cancellationToken)
    {
        var source = context.Certificates.AsNoTracking().AsQueryable();
        if (query.IsPublished.HasValue)
            source = source.Where(certificate => certificate.IsPublished == query.IsPublished.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
            source = source.Where(certificate =>
                EF.Functions.ILike(certificate.Issuer, $"%{query.Search}%") ||
                certificate.Translations.Any(translation =>
                    EF.Functions.ILike(translation.Name, $"%{query.Search}%")));

        var total = await source.LongCountAsync(cancellationToken);
        var items = await source
            .OrderBy(certificate => certificate.DisplayOrder)
            .ThenByDescending(certificate => certificate.IssuedDate)
            .ThenBy(certificate => certificate.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(certificate => certificate.Translations)
            .Include(certificate => certificate.Technologies)
            .AsSplitQuery()
            .ToArrayAsync(cancellationToken);
        return new CertificateEntityPage(items, total, query.Page, query.PageSize);
    }

    public Task<Certificate?> GetAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<Certificate> query = context.Certificates
            .Include(certificate => certificate.Translations)
            .Include(certificate => certificate.Technologies)
            .AsSplitQuery();
        if (!tracked) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(certificate => certificate.Id == id, cancellationToken);
    }

    public async Task<bool> TechnologyIdsExistAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return true;
        return await context.Technologies.AsNoTracking()
            .CountAsync(technology => ids.Contains(technology.Id), cancellationToken) == ids.Count;
    }

    public async Task AddAsync(Certificate certificate, CancellationToken cancellationToken) =>
        await context.Certificates.AddAsync(certificate, cancellationToken);

    public void Remove(Certificate certificate) => context.Certificates.Remove(certificate);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await context.SaveChangesAsync(cancellationToken);
}
