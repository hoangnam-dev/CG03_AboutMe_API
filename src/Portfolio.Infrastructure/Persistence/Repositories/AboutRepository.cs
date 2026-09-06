using Microsoft.EntityFrameworkCore;
using Portfolio.Application.About;

namespace Portfolio.Infrastructure.Persistence.Repositories;

public sealed class AboutRepository(PortfolioDbContext context) : IAboutRepository
{
    public Task<AboutPublicProjection?> GetPublicAsync(
        string slug, string locale, CancellationToken cancellationToken) =>
        context.Abouts.AsNoTracking()
            .Where(about => about.IsPublished && about.Profile.Slug == slug)
            .SelectMany(about => about.Translations
                .Where(translation => translation.LocaleCode == locale)
                .Select(translation => new AboutPublicProjection(
                    translation.Content,
                    translation.CareerGoal,
                    about.YearsOfExperience,
                    about.ProjectCount,
                    about.TechnologyCount,
                    about.ShowYearsOfExperience,
                    about.ShowProjectCount,
                    about.ShowTechnologyCount,
                    about.ShowContactSection)))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Portfolio.Application.Common.Models.About?> GetAdminAsync(
        CancellationToken cancellationToken) =>
        context.Abouts.AsNoTracking().Include(about => about.Translations)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Portfolio.Application.Common.Models.About?> GetForUpdateAsync(
        CancellationToken cancellationToken) =>
        context.Abouts.Include(about => about.Translations)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Guid?> GetProfileIdAsync(CancellationToken cancellationToken) =>
        context.Profiles.AsNoTracking().Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task AddAsync(
        Portfolio.Application.Common.Models.About about,
        CancellationToken cancellationToken) =>
        await context.Abouts.AddAsync(about, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await context.SaveChangesAsync(cancellationToken);
}
