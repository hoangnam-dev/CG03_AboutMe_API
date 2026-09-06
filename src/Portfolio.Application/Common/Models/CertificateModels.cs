namespace Portfolio.Application.Common.Models;

public sealed class Certificate
{
    public Guid Id { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public DateOnly IssuedDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public string? CredentialId { get; set; }
    public string? CredentialUrl { get; set; }
    public bool ShowCredentialId { get; set; }
    public string? FileUrl { get; set; }
    public string? ImageUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<CertificateTranslation> Translations { get; } = [];
    public ICollection<CertificateTechnology> Technologies { get; } = [];
}

public sealed class CertificateTranslation
{
    public Guid CertificateId { get; set; }
    public string LocaleCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Certificate Certificate { get; set; } = null!;
}

public sealed class CertificateTechnology
{
    public Guid CertificateId { get; set; }
    public Guid TechnologyId { get; set; }
    public Certificate Certificate { get; set; } = null!;
    public Technology Technology { get; set; } = null!;
}
