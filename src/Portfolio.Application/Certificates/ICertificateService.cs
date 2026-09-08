namespace Portfolio.Application.Certificates;

public interface ICertificateService
{
    Task<IReadOnlyList<CertificatePublicResponse>> GetPublicAsync(string slug, string? locale, CancellationToken cancellationToken);
    Task<CertificateAdminPage> GetCertificatesAsync(CertificateAdminQuery query, CancellationToken cancellationToken);
    Task<CertificateAdminResponse> GetCertificateAsync(Guid id, CancellationToken cancellationToken);
    Task<CertificateAdminResponse> CreateCertificateAsync(CertificateWriteRequest request, CancellationToken cancellationToken);
    Task<CertificateAdminResponse> UpdateCertificateAsync(Guid id, CertificateWriteRequest request, CancellationToken cancellationToken);
    Task DeleteCertificateAsync(Guid id, CancellationToken cancellationToken);
    Task<CertificateEvidenceResponse> UploadEvidenceAsync(Guid id, CertificateEvidenceUpload upload, CancellationToken cancellationToken);
}
