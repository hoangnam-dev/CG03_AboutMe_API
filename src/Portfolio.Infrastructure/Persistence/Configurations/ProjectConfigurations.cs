using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Application.Common.Models;

namespace Portfolio.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable(
            "projects",
            table =>
            {
                table.HasCheckConstraint("ck_projects_kind", "kind IN ('personal', 'professional')");
                table.HasCheckConstraint("ck_projects_disclosure_level", "disclosure_level IN ('full', 'limited')");
                table.HasCheckConstraint("ck_projects_display_order", "display_order >= 0");
                table.HasCheckConstraint("ck_projects_dates", "end_date IS NULL OR start_date IS NULL OR end_date >= start_date");
            });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(220).IsRequired();
        builder.Property(x => x.InternalName).HasColumnName("internal_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(20).HasConversion(
            value => value.ToString().ToLowerInvariant(),
            value => Enum.Parse<ProjectKind>(value, true)).HasDefaultValue(ProjectKind.Personal);
        builder.Property(x => x.DisclosureLevel).HasColumnName("disclosure_level").HasMaxLength(20).HasConversion(
            value => value.ToString().ToLowerInvariant(),
            value => Enum.Parse<ProjectDisclosureLevel>(value, true)).HasDefaultValue(ProjectDisclosureLevel.Full);
        builder.Property(x => x.ThumbnailUrl).HasColumnName("thumbnail_url");
        builder.Property(x => x.RepositoryUrl).HasColumnName("repository_url");
        builder.Property(x => x.DemoUrl).HasColumnName("demo_url");
        builder.Property(x => x.StartDate).HasColumnName("start_date");
        builder.Property(x => x.EndDate).HasColumnName("end_date");
        builder.Property(x => x.IsFeatured).HasColumnName("is_featured").HasDefaultValue(false);
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.Property(x => x.IsPublished).HasColumnName("is_published").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => new { x.Kind, x.DisclosureLevel, x.IsFeatured, x.DisplayOrder })
            .HasDatabaseName("idx_projects_public_filter_order")
            .HasFilter("is_published")
            .IsDescending(false, false, true, false);
    }
}

internal sealed class ProjectTranslationConfiguration : IEntityTypeConfiguration<ProjectTranslation>
{
    public void Configure(EntityTypeBuilder<ProjectTranslation> builder)
    {
        builder.ToTable(
            "project_translations",
            table => table.HasCheckConstraint("ck_project_translations_locale", "locale_code IN ('en', 'vi')"));
        builder.HasKey(x => new { x.ProjectId, x.LocaleCode });
        builder.Property(x => x.ProjectId).HasColumnName("project_id");
        builder.Property(x => x.LocaleCode).HasColumnName("locale_code").HasMaxLength(10);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ShortDescription).HasColumnName("short_description").HasMaxLength(500);
        builder.Property(x => x.ClientContext).HasColumnName("client_context");
        builder.Property(x => x.Role).HasColumnName("role").HasMaxLength(200);
        builder.Property(x => x.Problem).HasColumnName("problem");
        builder.Property(x => x.Solution).HasColumnName("solution");
        builder.Property(x => x.Result).HasColumnName("result");
        builder.HasOne(x => x.Project).WithMany(x => x.Translations).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProjectTechnologyConfiguration : IEntityTypeConfiguration<ProjectTechnology>
{
    public void Configure(EntityTypeBuilder<ProjectTechnology> builder)
    {
        builder.ToTable(
            "project_technologies",
            table => table.HasCheckConstraint("ck_project_technologies_display_order", "display_order >= 0"));
        builder.HasKey(x => new { x.ProjectId, x.TechnologyId });
        builder.Property(x => x.ProjectId).HasColumnName("project_id");
        builder.Property(x => x.TechnologyId).HasColumnName("technology_id");
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.HasOne(x => x.Project).WithMany(x => x.Technologies).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Technology).WithMany(x => x.Projects).HasForeignKey(x => x.TechnologyId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProjectHighlightConfiguration : IEntityTypeConfiguration<ProjectHighlight>
{
    public void Configure(EntityTypeBuilder<ProjectHighlight> builder)
    {
        builder.ToTable(
            "project_highlights",
            table =>
            {
                table.HasCheckConstraint("ck_project_highlights_locale", "locale_code IN ('en', 'vi')");
                table.HasCheckConstraint("ck_project_highlights_display_order", "display_order >= 0");
            });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProjectId).HasColumnName("project_id");
        builder.Property(x => x.LocaleCode).HasColumnName("locale_code").HasMaxLength(10);
        builder.Property(x => x.Content).HasColumnName("content").IsRequired();
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.HasIndex(x => new { x.ProjectId, x.LocaleCode, x.DisplayOrder }).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.LocaleCode, x.DisplayOrder }).HasDatabaseName("idx_project_highlights_order");
        builder.HasOne(x => x.Project).WithMany(x => x.Highlights).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProjectImageConfiguration : IEntityTypeConfiguration<ProjectImage>
{
    public void Configure(EntityTypeBuilder<ProjectImage> builder)
    {
        builder.ToTable(
            "project_images",
            table => table.HasCheckConstraint("ck_project_images_display_order", "display_order >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProjectId).HasColumnName("project_id");
        builder.Property(x => x.ImageUrl).HasColumnName("image_url").IsRequired();
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        builder.HasIndex(x => new { x.ProjectId, x.DisplayOrder }).HasDatabaseName("idx_project_images_order");
        builder.HasOne(x => x.Project).WithMany(x => x.Images).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProjectImageTranslationConfiguration : IEntityTypeConfiguration<ProjectImageTranslation>
{
    public void Configure(EntityTypeBuilder<ProjectImageTranslation> builder)
    {
        builder.ToTable(
            "project_image_translations",
            table => table.HasCheckConstraint("ck_project_image_translations_locale", "locale_code IN ('en', 'vi')"));
        builder.HasKey(x => new { x.ProjectImageId, x.LocaleCode });
        builder.Property(x => x.ProjectImageId).HasColumnName("project_image_id");
        builder.Property(x => x.LocaleCode).HasColumnName("locale_code").HasMaxLength(10);
        builder.Property(x => x.AltText).HasColumnName("alt_text").HasMaxLength(500).IsRequired();
        builder.HasOne(x => x.ProjectImage).WithMany(x => x.Translations).HasForeignKey(x => x.ProjectImageId).OnDelete(DeleteBehavior.Cascade);
    }
}
