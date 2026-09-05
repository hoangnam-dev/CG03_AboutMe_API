using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Portfolio.Application.Common.Models;
using Portfolio.Infrastructure.Authentication;

namespace Portfolio.Infrastructure.Persistence;

public sealed class PortfolioDbContext(
    DbContextOptions<PortfolioDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<ProfileTranslation> ProfileTranslations => Set<ProfileTranslation>();
    public DbSet<SocialLink> SocialLinks => Set<SocialLink>();
    public DbSet<About> Abouts => Set<About>();
    public DbSet<AboutTranslation> AboutTranslations => Set<AboutTranslation>();
    public DbSet<SkillCategory> SkillCategories => Set<SkillCategory>();
    public DbSet<SkillCategoryTranslation> SkillCategoryTranslations => Set<SkillCategoryTranslation>();
    public DbSet<Technology> Technologies => Set<Technology>();
    public DbSet<TechnologyTranslation> TechnologyTranslations => Set<TechnologyTranslation>();
    public DbSet<WorkExperience> WorkExperiences => Set<WorkExperience>();
    public DbSet<ExperienceTranslation> ExperienceTranslations => Set<ExperienceTranslation>();
    public DbSet<ExperienceHighlight> ExperienceHighlights => Set<ExperienceHighlight>();
    public DbSet<ExperienceTechnology> ExperienceTechnologies => Set<ExperienceTechnology>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTranslation> ProjectTranslations => Set<ProjectTranslation>();
    public DbSet<ProjectTechnology> ProjectTechnologies => Set<ProjectTechnology>();
    public DbSet<ProjectHighlight> ProjectHighlights => Set<ProjectHighlight>();
    public DbSet<ProjectImage> ProjectImages => Set<ProjectImage>();
    public DbSet<ProjectImageTranslation> ProjectImageTranslations => Set<ProjectImageTranslation>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<CertificateTranslation> CertificateTranslations => Set<CertificateTranslation>();
    public DbSet<CertificateTechnology> CertificateTechnologies => Set<CertificateTechnology>();
    public DbSet<ResumeVersionCounter> ResumeVersionCounters => Set<ResumeVersionCounter>();
    public DbSet<ResumeFile> Resumes => Set<ResumeFile>();
    public DbSet<ResumeTranslation> ResumeTranslations => Set<ResumeTranslation>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(PortfolioDbContext).Assembly);
    }
}
