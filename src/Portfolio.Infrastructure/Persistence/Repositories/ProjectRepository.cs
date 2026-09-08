using System.Data;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Projects;

namespace Portfolio.Infrastructure.Persistence.Repositories;

public sealed class ProjectRepository(PortfolioDbContext context) : IProjectRepository
{
    public async Task<IReadOnlyList<ProjectPublicProjection>?> GetPublicListAsync(
        string slug, string locale, CancellationToken cancellationToken)
    {
        if (!await PortfolioExistsAsync(slug, cancellationToken)) return null;
        return await PublicQuery(locale)
            .OrderByDescending(project => project.IsFeatured)
            .ThenBy(project => project.DisplayOrder)
            .ThenBy(project => project.Id)
            .Select(PublicProjection(locale, includeDetail: false))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ProjectPublicProjection?> GetPublicDetailAsync(
        string slug, string projectSlug, string locale, CancellationToken cancellationToken)
    {
        if (!await PortfolioExistsAsync(slug, cancellationToken)) return null;
        return await PublicQuery(locale)
            .Where(project => project.Slug == projectSlug)
            .Select(PublicProjection(locale, includeDetail: true))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ProjectEntityPage> GetProjectsAsync(
        ProjectAdminQuery query, CancellationToken cancellationToken)
    {
        var source = context.Projects.AsNoTracking().AsQueryable();
        if (query.Kind.HasValue) source = source.Where(project => project.Kind == query.Kind.Value);
        if (query.IsPublished.HasValue)
            source = source.Where(project => project.IsPublished == query.IsPublished.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
            source = source.Where(project =>
                EF.Functions.ILike(project.InternalName, $"%{query.Search}%") ||
                project.Translations.Any(translation =>
                    EF.Functions.ILike(translation.Name, $"%{query.Search}%")));

        var total = await source.LongCountAsync(cancellationToken);
        var projects = await source
            .OrderBy(project => project.DisplayOrder)
            .ThenBy(project => project.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(project => project.Translations)
            .Include(project => project.Highlights)
            .Include(project => project.Technologies)
            .Include(project => project.Images)
                .ThenInclude(image => image.Translations)
            .AsSplitQuery()
            .ToArrayAsync(cancellationToken);
        return new ProjectEntityPage(projects, total, query.Page, query.PageSize);
    }

    public Task<Project?> GetAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<Project> query = context.Projects
            .Include(project => project.Translations)
            .Include(project => project.Highlights)
            .Include(project => project.Technologies)
            .Include(project => project.Images)
                .ThenInclude(image => image.Translations)
            .AsSplitQuery();
        if (!tracked) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(project => project.Id == id, cancellationToken);
    }

    public Task<bool> SlugExistsAsync(
        string slug, Guid? excludedId, CancellationToken cancellationToken) =>
        context.Projects.AsNoTracking().AnyAsync(
            project => project.Slug == slug && (!excludedId.HasValue || project.Id != excludedId.Value),
            cancellationToken);

    public async Task<bool> TechnologyIdsExistAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return true;
        return await context.Technologies.AsNoTracking()
            .CountAsync(technology => ids.Contains(technology.Id), cancellationToken) == ids.Count;
    }

    public async Task AddAsync(Project project, CancellationToken cancellationToken) =>
        await context.Projects.AddAsync(project, cancellationToken);

    public void Remove(Project project) => context.Projects.Remove(project);

    public async Task<ProjectReorderResult> ReorderAsync(
        IReadOnlyList<ProjectOrderItem> items, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var projects = await context.Projects.ToArrayAsync(cancellationToken);
        var requestedIds = items.Select(item => item.Id).ToHashSet();
        if (projects.Length != requestedIds.Count || projects.Any(project => !requestedIds.Contains(project.Id)))
            return ProjectReorderResult.SetMismatch;
        var orders = items.ToDictionary(item => item.Id, item => item.DisplayOrder);
        foreach (var project in projects) project.DisplayOrder = orders[project.Id];
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ProjectReorderResult.Success;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await context.SaveChangesAsync(cancellationToken);

    private Task<bool> PortfolioExistsAsync(string slug, CancellationToken cancellationToken) =>
        context.Profiles.AsNoTracking().AnyAsync(profile => profile.Slug == slug, cancellationToken);

    private IQueryable<Project> PublicQuery(string locale) => context.Projects.AsNoTracking()
        .Where(project => project.IsPublished &&
            project.Translations.Any(translation => translation.LocaleCode == locale));

    private static Expression<Func<Project, ProjectPublicProjection>> PublicProjection(
        string locale, bool includeDetail) => project => new(
            project.Id,
            project.Slug,
            project.Translations.Where(item => item.LocaleCode == locale).Select(item => item.Name).Single(),
            project.Translations.Where(item => item.LocaleCode == locale).Select(item => item.ShortDescription).Single(),
            project.Kind,
            project.DisclosureLevel,
            project.ThumbnailUrl,
            includeDetail && project.DisclosureLevel == ProjectDisclosureLevel.Full ? project.RepositoryUrl : null,
            includeDetail && project.DisclosureLevel == ProjectDisclosureLevel.Full ? project.DemoUrl : null,
            project.StartDate,
            project.EndDate,
            project.IsFeatured,
            includeDetail && project.DisclosureLevel == ProjectDisclosureLevel.Full
                ? project.Translations.Where(item => item.LocaleCode == locale).Select(item => item.ClientContext).Single()
                : null,
            project.Translations.Where(item => item.LocaleCode == locale).Select(item => item.Role).Single(),
            includeDetail && project.DisclosureLevel == ProjectDisclosureLevel.Full
                ? project.Translations.Where(item => item.LocaleCode == locale).Select(item => item.Problem).Single()
                : null,
            includeDetail && project.DisclosureLevel == ProjectDisclosureLevel.Full
                ? project.Translations.Where(item => item.LocaleCode == locale).Select(item => item.Solution).Single()
                : null,
            includeDetail && project.DisclosureLevel == ProjectDisclosureLevel.Full
                ? project.Translations.Where(item => item.LocaleCode == locale).Select(item => item.Result).Single()
                : null,
            includeDetail
                ? project.Highlights.Where(item => item.LocaleCode == locale)
                    .OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id)
                    .Select(item => new ProjectHighlightResponse(item.Id, item.Content, item.DisplayOrder)).ToArray()
                : Array.Empty<ProjectHighlightResponse>(),
            project.Technologies
                .Where(link => link.Technology.IsPublished && link.Technology.Category.IsPublished &&
                    link.Technology.Translations.Any(item => item.LocaleCode == locale))
                .OrderBy(link => link.DisplayOrder).ThenBy(link => link.TechnologyId)
                .Select(link => new ProjectTechnologyResponse(
                    link.TechnologyId,
                    link.Technology.Translations.Where(item => item.LocaleCode == locale).Select(item => item.Name).Single(),
                    link.Technology.IconType == SkillIconType.Lucide ? "lucide" :
                        link.Technology.IconType == SkillIconType.Image ? "image" : "text",
                    link.Technology.IconValue,
                    link.DisplayOrder))
                .ToArray(),
            includeDetail && project.DisclosureLevel == ProjectDisclosureLevel.Full
                ? project.Images.OrderBy(image => image.DisplayOrder).ThenBy(image => image.Id)
                    .Where(image => image.Translations.Any(item => item.LocaleCode == locale))
                    .Select(image => new ProjectPublicImageProjection(
                        image.Id,
                        image.ImageUrl,
                        image.Translations.Where(item => item.LocaleCode == locale).Select(item => item.AltText).Single(),
                        image.DisplayOrder))
                    .ToArray()
                : Array.Empty<ProjectPublicImageProjection>());
}
