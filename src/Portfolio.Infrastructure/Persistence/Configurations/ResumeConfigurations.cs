using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Application.Common.Models;

namespace Portfolio.Infrastructure.Persistence.Configurations;

internal sealed class ResumeVersionCounterConfiguration : IEntityTypeConfiguration<ResumeVersionCounter>
{
    public void Configure(EntityTypeBuilder<ResumeVersionCounter> builder)
    {
        builder.ToTable(
            "resume_version_counters",
            table =>
            {
                table.HasCheckConstraint("ck_resume_version_counters_locale", "language_code IN ('en', 'vi')");
                table.HasCheckConstraint("ck_resume_version_counters_year", "version_year BETWEEN 2000 AND 9999");
                table.HasCheckConstraint("ck_resume_version_counters_sequence", "last_sequence > 0");
            });
        builder.HasKey(x => new { x.LanguageCode, x.VersionYear });
        builder.Property(x => x.LanguageCode).HasColumnName("language_code").HasMaxLength(10);
        builder.Property(x => x.VersionYear).HasColumnName("version_year");
        builder.Property(x => x.LastSequence).HasColumnName("last_sequence");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
    }
}

internal sealed class ResumeConfiguration : IEntityTypeConfiguration<ResumeFile>
{
    public void Configure(EntityTypeBuilder<ResumeFile> builder)
    {
        builder.ToTable(
            "resumes",
            table =>
            {
                table.HasCheckConstraint("ck_resumes_locale", "language_code IN ('en', 'vi')");
                table.HasCheckConstraint("ck_resumes_file_size", "file_size > 0");
                table.HasCheckConstraint("ck_resumes_year", "version_year BETWEEN 2000 AND 9999");
                table.HasCheckConstraint("ck_resumes_sequence", "version_sequence > 0");
                table.HasCheckConstraint("ck_resumes_display_order", "display_order >= 0");
            });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.LanguageCode).HasColumnName("language_code").HasMaxLength(10);
        builder.Property(x => x.FileUrl).HasColumnName("file_url").IsRequired();
        builder.Property(x => x.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.FileSize).HasColumnName("file_size");
        builder.Property(x => x.VersionYear).HasColumnName("version_year");
        builder.Property(x => x.VersionSequence).HasColumnName("version_sequence");
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.Property(x => x.IsPublished).HasColumnName("is_published").HasDefaultValue(true);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        builder.HasAlternateKey(x => new { x.LanguageCode, x.VersionYear, x.VersionSequence })
            .HasName("ak_resumes_language_year_sequence");
        builder.HasIndex(x => x.LanguageCode)
            .HasDatabaseName("ux_resumes_one_active_per_language")
            .HasFilter("is_active")
            .IsUnique();
        builder.HasIndex(x => new { x.LanguageCode, x.VersionYear, x.VersionSequence })
            .HasDatabaseName("idx_resumes_public_language_version")
            .HasFilter("is_published")
            .IsDescending(false, true, true);
        builder.HasOne(x => x.VersionCounter)
            .WithMany(x => x.Resumes)
            .HasForeignKey(x => new { x.LanguageCode, x.VersionYear })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ResumeTranslationConfiguration : IEntityTypeConfiguration<ResumeTranslation>
{
    public void Configure(EntityTypeBuilder<ResumeTranslation> builder)
    {
        builder.ToTable(
            "resume_translations",
            table => table.HasCheckConstraint("ck_resume_translations_locale", "locale_code IN ('en', 'vi')"));
        builder.HasKey(x => new { x.ResumeId, x.LocaleCode });
        builder.Property(x => x.ResumeId).HasColumnName("resume_id");
        builder.Property(x => x.LocaleCode).HasColumnName("locale_code").HasMaxLength(10);
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        builder.HasOne(x => x.Resume).WithMany(x => x.Translations).HasForeignKey(x => x.ResumeId).OnDelete(DeleteBehavior.Cascade);
    }
}
