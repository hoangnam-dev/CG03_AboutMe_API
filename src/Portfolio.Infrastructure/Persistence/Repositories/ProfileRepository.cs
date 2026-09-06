using Microsoft.EntityFrameworkCore;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Profiles;

namespace Portfolio.Infrastructure.Persistence.Repositories;

public sealed class ProfileRepository(PortfolioDbContext context) : IProfileRepository
{
    public Task<ProfilePublicProjection?> GetPublicAsync(
        string slug, string locale, CancellationToken cancellationToken) =>
        context.Profiles.AsNoTracking()
            .Where(profile => profile.Slug == slug)
            .SelectMany(profile => profile.Translations
                .Where(translation => translation.LocaleCode == locale)
                .Select(translation => new ProfilePublicProjection(
                    profile.Slug,
                    profile.FullName,
                    translation.Title,
                    translation.ShortBio,
                    translation.Location,
                    translation.Availability,
                    profile.AvailableForWork,
                    profile.Email,
                    profile.Phone,
                    profile.ShowEmail,
                    profile.ShowPhone,
                    profile.AvatarUrl,
                    profile.HeroImageUrl,
                    profile.SocialLinks.Where(link => link.IsPublished)
                        .OrderBy(link => link.DisplayOrder).ThenBy(link => link.Id)
                        .Select(link => new PublicSocialLink(
                            link.Platform, link.Label, link.Url, link.IconName, link.DisplayOrder))
                        .ToList())))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Profile?> GetAdminAsync(CancellationToken cancellationToken) =>
        QueryAggregate(false).SingleOrDefaultAsync(cancellationToken);

    public Task<Profile?> GetForUpdateAsync(CancellationToken cancellationToken) =>
        QueryAggregate(true).SingleOrDefaultAsync(cancellationToken);

    public Task<bool> SlugExistsAsync(
        string slug, Guid? excludedId, CancellationToken cancellationToken) =>
        context.Profiles.AnyAsync(
            profile => profile.Slug == slug && (!excludedId.HasValue || profile.Id != excludedId.Value),
            cancellationToken);

    public async Task AddAsync(Profile profile, CancellationToken cancellationToken) =>
        await context.Profiles.AddAsync(profile, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await context.SaveChangesAsync(cancellationToken);

    private IQueryable<Profile> QueryAggregate(bool tracking)
    {
        IQueryable<Profile> query = context.Profiles
            .Include(profile => profile.Translations)
            .Include(profile => profile.SocialLinks)
            .AsSplitQuery();
        return tracking ? query : query.AsNoTracking();
    }
}
