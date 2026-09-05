using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Application.Common.Models;

namespace Portfolio.Infrastructure.Persistence.Configurations;

internal sealed class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.ToTable("profiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(220).IsRequired();
        builder.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(320);
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(30);
        builder.Property(x => x.AvatarUrl).HasColumnName("avatar_url");
        builder.Property(x => x.HeroImageUrl).HasColumnName("hero_image_url");
        builder.Property(x => x.AvailableForWork).HasColumnName("available_for_work").HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        builder.HasIndex(x => x.Slug).IsUnique();
    }
}

internal sealed class ProfileTranslationConfiguration : IEntityTypeConfiguration<ProfileTranslation>
{
    public void Configure(EntityTypeBuilder<ProfileTranslation> builder)
    {
        builder.ToTable(
            "profile_translations",
            table => table.HasCheckConstraint("ck_profile_translations_locale", "locale_code IN ('en', 'vi')"));
        builder.HasKey(x => new { x.ProfileId, x.LocaleCode });
        builder.Property(x => x.ProfileId).HasColumnName("profile_id");
        builder.Property(x => x.LocaleCode).HasColumnName("locale_code").HasMaxLength(10);
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ShortBio).HasColumnName("short_bio").HasMaxLength(500);
        builder.Property(x => x.Location).HasColumnName("location").HasMaxLength(200);
        builder.Property(x => x.Availability).HasColumnName("availability").HasMaxLength(250);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        builder.HasOne(x => x.Profile).WithMany(x => x.Translations).HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SocialLinkConfiguration : IEntityTypeConfiguration<SocialLink>
{
    public void Configure(EntityTypeBuilder<SocialLink> builder)
    {
        builder.ToTable(
            "social_links",
            table => table.HasCheckConstraint("ck_social_links_display_order", "display_order >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProfileId).HasColumnName("profile_id");
        builder.Property(x => x.Platform).HasColumnName("platform").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Label).HasColumnName("label").HasMaxLength(100);
        builder.Property(x => x.Url).HasColumnName("url").IsRequired();
        builder.Property(x => x.IconName).HasColumnName("icon_name").HasMaxLength(100);
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.Property(x => x.IsPublished).HasColumnName("is_published").HasDefaultValue(true);
        builder.HasIndex(x => new { x.ProfileId, x.Platform }).IsUnique();
        builder.HasIndex(x => x.DisplayOrder).HasDatabaseName("idx_social_links_public_order").HasFilter("is_published");
        builder.HasOne(x => x.Profile).WithMany(x => x.SocialLinks).HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AboutConfiguration : IEntityTypeConfiguration<About>
{
    public void Configure(EntityTypeBuilder<About> builder)
    {
        builder.ToTable(
            "abouts",
            table =>
            {
                table.HasCheckConstraint("ck_abouts_years_of_experience", "years_of_experience >= 0");
                table.HasCheckConstraint("ck_abouts_project_count", "project_count >= 0");
                table.HasCheckConstraint("ck_abouts_technology_count", "technology_count >= 0");
            });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProfileId).HasColumnName("profile_id");
        builder.Property(x => x.YearsOfExperience).HasColumnName("years_of_experience").HasPrecision(4, 1).HasDefaultValue(0m);
        builder.Property(x => x.ProjectCount).HasColumnName("project_count").HasDefaultValue(0);
        builder.Property(x => x.TechnologyCount).HasColumnName("technology_count").HasDefaultValue(0);
        builder.Property(x => x.ShowYearsOfExperience).HasColumnName("show_years_of_experience").HasDefaultValue(true);
        builder.Property(x => x.ShowProjectCount).HasColumnName("show_project_count").HasDefaultValue(true);
        builder.Property(x => x.ShowTechnologyCount).HasColumnName("show_technology_count").HasDefaultValue(true);
        builder.Property(x => x.ShowContactSection).HasColumnName("show_contact_section").HasDefaultValue(true);
        builder.Property(x => x.IsPublished).HasColumnName("is_published").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        builder.HasIndex(x => x.ProfileId).IsUnique();
        builder.HasOne(x => x.Profile).WithOne(x => x.About).HasForeignKey<About>(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AboutTranslationConfiguration : IEntityTypeConfiguration<AboutTranslation>
{
    public void Configure(EntityTypeBuilder<AboutTranslation> builder)
    {
        builder.ToTable(
            "about_translations",
            table => table.HasCheckConstraint("ck_about_translations_locale", "locale_code IN ('en', 'vi')"));
        builder.HasKey(x => new { x.AboutId, x.LocaleCode });
        builder.Property(x => x.AboutId).HasColumnName("about_id");
        builder.Property(x => x.LocaleCode).HasColumnName("locale_code").HasMaxLength(10);
        builder.Property(x => x.Content).HasColumnName("content");
        builder.Property(x => x.CareerGoal).HasColumnName("career_goal");
        builder.HasOne(x => x.About).WithMany(x => x.Translations).HasForeignKey(x => x.AboutId).OnDelete(DeleteBehavior.Cascade);
    }
}
