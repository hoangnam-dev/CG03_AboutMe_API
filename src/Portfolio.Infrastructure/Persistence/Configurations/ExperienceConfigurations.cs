using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Application.Common.Models;

namespace Portfolio.Infrastructure.Persistence.Configurations;

internal sealed class WorkExperienceConfiguration : IEntityTypeConfiguration<WorkExperience>
{
    public void Configure(EntityTypeBuilder<WorkExperience> builder)
    {
        builder.ToTable(
            "work_experiences",
            table =>
            {
                table.HasCheckConstraint("ck_work_experiences_display_order", "display_order >= 0");
                table.HasCheckConstraint("ck_work_experiences_dates", "end_date IS NULL OR end_date >= start_date");
            });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CompanyName).HasColumnName("company_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.EmploymentType).HasColumnName("employment_type").HasMaxLength(30);
        builder.Property(x => x.CompanyUrl).HasColumnName("company_url");
        builder.Property(x => x.StartDate).HasColumnName("start_date");
        builder.Property(x => x.EndDate).HasColumnName("end_date");
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.Property(x => x.IsPublished).HasColumnName("is_published").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        builder.HasIndex(x => new { x.DisplayOrder, x.StartDate })
            .HasDatabaseName("idx_work_experiences_public_order")
            .HasFilter("is_published")
            .IsDescending(false, true);
    }
}

internal sealed class ExperienceTranslationConfiguration : IEntityTypeConfiguration<ExperienceTranslation>
{
    public void Configure(EntityTypeBuilder<ExperienceTranslation> builder)
    {
        builder.ToTable(
            "experience_translations",
            table => table.HasCheckConstraint("ck_experience_translations_locale", "locale_code IN ('en', 'vi')"));
        builder.HasKey(x => new { x.ExperienceId, x.LocaleCode });
        builder.Property(x => x.ExperienceId).HasColumnName("experience_id");
        builder.Property(x => x.LocaleCode).HasColumnName("locale_code").HasMaxLength(10);
        builder.Property(x => x.Position).HasColumnName("position").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Location).HasColumnName("location").HasMaxLength(200);
        builder.Property(x => x.Description).HasColumnName("description");
        builder.HasOne(x => x.Experience).WithMany(x => x.Translations).HasForeignKey(x => x.ExperienceId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ExperienceHighlightConfiguration : IEntityTypeConfiguration<ExperienceHighlight>
{
    public void Configure(EntityTypeBuilder<ExperienceHighlight> builder)
    {
        builder.ToTable(
            "experience_highlights",
            table =>
            {
                table.HasCheckConstraint("ck_experience_highlights_locale", "locale_code IN ('en', 'vi')");
                table.HasCheckConstraint("ck_experience_highlights_type", "highlight_type IN ('responsibility', 'achievement')");
                table.HasCheckConstraint("ck_experience_highlights_display_order", "display_order >= 0");
            });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ExperienceId).HasColumnName("experience_id");
        builder.Property(x => x.LocaleCode).HasColumnName("locale_code").HasMaxLength(10);
        builder.Property(x => x.HighlightType).HasColumnName("highlight_type").HasMaxLength(20).HasConversion(
            value => value.ToString().ToLowerInvariant(),
            value => Enum.Parse<ExperienceHighlightType>(value, true));
        builder.Property(x => x.Content).HasColumnName("content").IsRequired();
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.HasIndex(x => new { x.ExperienceId, x.LocaleCode, x.HighlightType, x.DisplayOrder }).IsUnique();
        builder.HasIndex(x => new { x.ExperienceId, x.LocaleCode, x.HighlightType, x.DisplayOrder })
            .HasDatabaseName("idx_experience_highlights_order");
        builder.HasOne(x => x.Experience).WithMany(x => x.Highlights).HasForeignKey(x => x.ExperienceId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ExperienceTechnologyConfiguration : IEntityTypeConfiguration<ExperienceTechnology>
{
    public void Configure(EntityTypeBuilder<ExperienceTechnology> builder)
    {
        builder.ToTable(
            "experience_technologies",
            table => table.HasCheckConstraint("ck_experience_technologies_display_order", "display_order >= 0"));
        builder.HasKey(x => new { x.ExperienceId, x.TechnologyId });
        builder.Property(x => x.ExperienceId).HasColumnName("experience_id");
        builder.Property(x => x.TechnologyId).HasColumnName("technology_id");
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.HasOne(x => x.Experience).WithMany(x => x.Technologies).HasForeignKey(x => x.ExperienceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Technology).WithMany(x => x.Experiences).HasForeignKey(x => x.TechnologyId).OnDelete(DeleteBehavior.Restrict);
    }
}
