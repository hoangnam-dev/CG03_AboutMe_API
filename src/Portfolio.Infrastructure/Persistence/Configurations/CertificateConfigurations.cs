using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Application.Common.Models;

namespace Portfolio.Infrastructure.Persistence.Configurations;

internal sealed class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> builder)
    {
        builder.ToTable(
            "certificates",
            table =>
            {
                table.HasCheckConstraint("ck_certificates_display_order", "display_order >= 0");
                table.HasCheckConstraint(
                    "ck_certificates_dates",
                    "expiration_date IS NULL OR expiration_date >= issued_date");
            });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Issuer).HasColumnName("issuer").HasMaxLength(200).IsRequired();
        builder.Property(x => x.IssuedDate).HasColumnName("issued_date").IsRequired();
        builder.Property(x => x.ExpirationDate).HasColumnName("expiration_date");
        builder.Property(x => x.CredentialId).HasColumnName("credential_id").HasMaxLength(200);
        builder.Property(x => x.CredentialUrl).HasColumnName("credential_url");
        builder.Property(x => x.ShowCredentialId).HasColumnName("show_credential_id").HasDefaultValue(false);
        builder.Property(x => x.FileUrl).HasColumnName("file_url");
        builder.Property(x => x.ImageUrl).HasColumnName("image_url");
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.Property(x => x.IsPublished).HasColumnName("is_published").HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        builder.HasIndex(x => new { x.DisplayOrder, x.IssuedDate })
            .HasDatabaseName("idx_certificates_public_order")
            .HasFilter("is_published")
            .IsDescending(false, true);
    }
}

internal sealed class CertificateTranslationConfiguration : IEntityTypeConfiguration<CertificateTranslation>
{
    public void Configure(EntityTypeBuilder<CertificateTranslation> builder)
    {
        builder.ToTable(
            "certificate_translations",
            table => table.HasCheckConstraint("ck_certificate_translations_locale", "locale_code IN ('en', 'vi')"));
        builder.HasKey(x => new { x.CertificateId, x.LocaleCode });
        builder.Property(x => x.CertificateId).HasColumnName("certificate_id");
        builder.Property(x => x.LocaleCode).HasColumnName("locale_code").HasMaxLength(10);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Certificate).WithMany(x => x.Translations).HasForeignKey(x => x.CertificateId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CertificateTechnologyConfiguration : IEntityTypeConfiguration<CertificateTechnology>
{
    public void Configure(EntityTypeBuilder<CertificateTechnology> builder)
    {
        builder.ToTable("certificate_technologies");
        builder.HasKey(x => new { x.CertificateId, x.TechnologyId });
        builder.Property(x => x.CertificateId).HasColumnName("certificate_id");
        builder.Property(x => x.TechnologyId).HasColumnName("technology_id");
        builder.HasOne(x => x.Certificate).WithMany(x => x.Technologies).HasForeignKey(x => x.CertificateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Technology).WithMany(x => x.Certificates).HasForeignKey(x => x.TechnologyId).OnDelete(DeleteBehavior.Restrict);
    }
}
