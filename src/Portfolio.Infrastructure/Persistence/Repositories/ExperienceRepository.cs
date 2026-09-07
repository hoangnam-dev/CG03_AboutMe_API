using System.Data;
using Microsoft.EntityFrameworkCore;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Experiences;

namespace Portfolio.Infrastructure.Persistence.Repositories;

public sealed class ExperienceRepository(PortfolioDbContext context) : IExperienceRepository
{
    public async Task<IReadOnlyList<PublicExperienceResponse>?> GetPublicAsync(
        string slug, string locale, CancellationToken cancellationToken)
    {
        if (!await context.Profiles.AsNoTracking()
            .AnyAsync(profile => profile.Slug == slug, cancellationToken))
            return null;

        var experiences = await context.WorkExperiences.AsNoTracking()
            .Where(experience => experience.IsPublished &&
                experience.Translations.Any(item => item.LocaleCode == locale))
            .OrderBy(experience => experience.DisplayOrder)
            .ThenByDescending(experience => experience.StartDate)
            .ThenBy(experience => experience.Id)
            .Include(experience => experience.Translations.Where(item => item.LocaleCode == locale))
            .Include(experience => experience.Highlights.Where(item => item.LocaleCode == locale))
            .Include(experience => experience.Technologies.Where(link =>
                link.Technology.IsPublished &&
                link.Technology.Category.IsPublished &&
                link.Technology.Translations.Any(item => item.LocaleCode == locale)))
                .ThenInclude(link => link.Technology)
                    .ThenInclude(technology => technology.Translations.Where(item => item.LocaleCode == locale))
            .AsSplitQuery()
            .ToArrayAsync(cancellationToken);

        return experiences.Select(experience => new PublicExperienceResponse(
            experience.Id,
            experience.CompanyName,
            experience.EmploymentType,
            experience.CompanyUrl,
            experience.StartDate,
            experience.EndDate,
            experience.EndDate is null,
            experience.DisplayOrder,
            Map(experience.Translations.Single()),
            experience.Highlights
                .OrderBy(item => item.HighlightType)
                .ThenBy(item => item.DisplayOrder)
                .ThenBy(item => item.Id)
                .Select(Map)
                .ToArray(),
            experience.Technologies
                .Where(link => link.Technology.Translations.Count != 0)
                .OrderBy(link => link.DisplayOrder)
                .ThenBy(link => link.TechnologyId)
                .Select(link => new ExperienceTechnologyResponse(
                    link.TechnologyId,
                    link.Technology.Translations.Single().Name,
                    link.Technology.IconType.ToString().ToLowerInvariant(),
                    link.Technology.IconValue,
                    link.DisplayOrder))
                .ToArray()))
            .ToArray();
    }

    public async Task<ExperienceAdminPage> GetExperiencesAsync(
        ExperienceAdminQuery query, CancellationToken cancellationToken)
    {
        var source = context.WorkExperiences.AsNoTracking().AsQueryable();
        if (query.IsPublished.HasValue)
            source = source.Where(experience => experience.IsPublished == query.IsPublished.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
            source = source.Where(experience =>
                EF.Functions.ILike(experience.CompanyName, $"%{query.Search}%") ||
                experience.Translations.Any(translation =>
                    EF.Functions.ILike(translation.Position, $"%{query.Search}%")));

        var total = await source.LongCountAsync(cancellationToken);
        var experiences = await source
            .OrderBy(experience => experience.DisplayOrder)
            .ThenByDescending(experience => experience.StartDate)
            .ThenBy(experience => experience.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(experience => experience.Translations)
            .Include(experience => experience.Highlights)
            .Include(experience => experience.Technologies)
            .AsSplitQuery()
            .ToArrayAsync(cancellationToken);
        return new ExperienceAdminPage(
            experiences.Select(MapAdmin).ToArray(),
            total,
            query.Page,
            query.PageSize);
    }

    public Task<WorkExperience?> GetAsync(
        Guid id, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<WorkExperience> query = context.WorkExperiences
            .Include(experience => experience.Translations)
            .Include(experience => experience.Highlights)
            .Include(experience => experience.Technologies)
            .AsSplitQuery();
        if (!tracked) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(experience => experience.Id == id, cancellationToken);
    }

    public async Task<bool> TechnologyIdsExistAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return true;
        return await context.Technologies.AsNoTracking()
            .CountAsync(technology => ids.Contains(technology.Id), cancellationToken) == ids.Count;
    }

    public async Task AddAsync(WorkExperience experience, CancellationToken cancellationToken) =>
        await context.WorkExperiences.AddAsync(experience, cancellationToken);

    public void Remove(WorkExperience experience) => context.WorkExperiences.Remove(experience);

    public async Task<ExperienceReorderResult> ReorderAsync(
        IReadOnlyList<ExperienceOrderItem> items, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var experiences = await context.WorkExperiences.ToArrayAsync(cancellationToken);
        var requestedIds = items.Select(item => item.Id).ToHashSet();
        if (experiences.Length != requestedIds.Count ||
            experiences.Any(experience => !requestedIds.Contains(experience.Id)))
            return ExperienceReorderResult.SetMismatch;

        var orders = items.ToDictionary(item => item.Id, item => item.DisplayOrder);
        foreach (var experience in experiences)
            experience.DisplayOrder = orders[experience.Id];
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ExperienceReorderResult.Success;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await context.SaveChangesAsync(cancellationToken);

    private static ExperienceTranslationRequest Map(ExperienceTranslation translation) =>
        new(translation.Position, translation.Location, translation.Description);

    private static ExperienceHighlightResponse Map(ExperienceHighlight highlight) => new(
        highlight.Id,
        highlight.LocaleCode,
        highlight.HighlightType.ToString().ToLowerInvariant(),
        highlight.Content,
        highlight.DisplayOrder);

    private static ExperienceAdminResponse MapAdmin(WorkExperience experience) => new(
        experience.Id,
        experience.CompanyName,
        experience.EmploymentType,
        experience.CompanyUrl,
        experience.StartDate,
        experience.EndDate,
        experience.EndDate is null,
        experience.DisplayOrder,
        experience.IsPublished,
        experience.Translations.ToDictionary(
            item => item.LocaleCode,
            item => Map(item),
            StringComparer.Ordinal),
        experience.Highlights
            .OrderBy(item => item.LocaleCode)
            .ThenBy(item => item.HighlightType)
            .ThenBy(item => item.DisplayOrder)
            .Select(Map)
            .ToArray(),
        experience.Technologies
            .OrderBy(item => item.DisplayOrder)
            .Select(item => item.TechnologyId)
            .ToArray());
}
