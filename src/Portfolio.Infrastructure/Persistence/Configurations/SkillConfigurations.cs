using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Application.Common.Models;

namespace Portfolio.Infrastructure.Persistence.Configurations;

internal sealed class SkillCategoryConfiguration : IEntityTypeConfiguration<SkillCategory>
{
    public void Configure(EntityTypeBuilder<SkillCategory> builder)
    {
        builder.ToTable(
            "skill_categories",
            table => table.HasCheckConstraint("ck_skill_categories_display_order", "display_order >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.Property(x => x.IsPublished).HasColumnName("is_published").HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        builder.HasIndex(x => x.DisplayOrder).HasDatabaseName("idx_skill_categories_public_order").HasFilter("is_published");
    }
}

internal sealed class SkillCategoryTranslationConfiguration : IEntityTypeConfiguration<SkillCategoryTranslation>
{
    public void Configure(EntityTypeBuilder<SkillCategoryTranslation> builder)
    {
        builder.ToTable(
            "skill_category_translations",
            table => table.HasCheckConstraint("ck_skill_category_translations_locale", "locale_code IN ('en', 'vi')"));
        builder.HasKey(x => new { x.CategoryId, x.LocaleCode });
        builder.Property(x => x.CategoryId).HasColumnName("category_id");
        builder.Property(x => x.LocaleCode).HasColumnName("locale_code").HasMaxLength(10);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.HasOne(x => x.Category).WithMany(x => x.Translations).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TechnologyConfiguration : IEntityTypeConfiguration<Technology>
{
    public void Configure(EntityTypeBuilder<Technology> builder)
    {
        builder.ToTable(
            "technologies",
            table =>
            {
                table.HasCheckConstraint("ck_technologies_skill_level", "skill_level IN ('Primary', 'Experienced', 'Familiar', 'Learning')");
                table.HasCheckConstraint("ck_technologies_years_of_experience", "years_of_experience IS NULL OR years_of_experience >= 0");
                table.HasCheckConstraint("ck_technologies_icon_type", "icon_type IN ('lucide', 'image', 'text')");
                table.HasCheckConstraint(
                    "ck_technologies_icon_value",
                    "(icon_type = 'text' AND char_length(btrim(icon_value)) BETWEEN 1 AND 6) OR " +
                    "(icon_type = 'lucide' AND char_length(btrim(icon_value)) BETWEEN 1 AND 100 AND icon_value ~ '^[a-z0-9]+(-[a-z0-9]+)*$') OR " +
                    "(icon_type = 'image' AND char_length(btrim(icon_value)) BETWEEN 1 AND 500)");
                table.HasCheckConstraint("ck_technologies_display_order", "display_order >= 0");
            });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CategoryId).HasColumnName("category_id");
        builder.Property(x => x.SkillLevel).HasColumnName("skill_level").HasMaxLength(20).HasConversion<string>();
        builder.Property(x => x.YearsOfExperience).HasColumnName("years_of_experience").HasPrecision(4, 1);
        builder.Property(x => x.IconType).HasColumnName("icon_type").HasMaxLength(20).HasConversion(
            value => value.ToString().ToLowerInvariant(),
            value => Enum.Parse<SkillIconType>(value, true))
            .HasDefaultValue(SkillIconType.Text)
            .HasSentinel((SkillIconType)(-1));
        builder.Property(x => x.IconValue).HasColumnName("icon_value").HasMaxLength(500).IsRequired();
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.Property(x => x.IsPublished).HasColumnName("is_published").HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        builder.HasIndex(x => new { x.CategoryId, x.DisplayOrder }).HasDatabaseName("idx_technologies_public_category_order").HasFilter("is_published");
        builder.HasOne(x => x.Category).WithMany(x => x.Technologies).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TechnologyTranslationConfiguration : IEntityTypeConfiguration<TechnologyTranslation>
{
    public void Configure(EntityTypeBuilder<TechnologyTranslation> builder)
    {
        builder.ToTable(
            "technology_translations",
            table => table.HasCheckConstraint("ck_technology_translations_locale", "locale_code IN ('en', 'vi')"));
        builder.HasKey(x => new { x.TechnologyId, x.LocaleCode });
        builder.Property(x => x.TechnologyId).HasColumnName("technology_id");
        builder.Property(x => x.LocaleCode).HasColumnName("locale_code").HasMaxLength(10);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.LocaleCode, x.Name }).IsUnique();
        builder.HasOne(x => x.Technology).WithMany(x => x.Translations).HasForeignKey(x => x.TechnologyId).OnDelete(DeleteBehavior.Cascade);
    }
}
