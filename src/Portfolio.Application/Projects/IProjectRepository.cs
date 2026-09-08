using Portfolio.Application.Common.Models;

namespace Portfolio.Application.Projects;

public interface IProjectRepository
{
    Task<IReadOnlyList<ProjectPublicProjection>?> GetPublicListAsync(string slug, string locale, CancellationToken cancellationToken);
    Task<ProjectPublicProjection?> GetPublicDetailAsync(string slug, string projectSlug, string locale, CancellationToken cancellationToken);
    Task<ProjectEntityPage> GetProjectsAsync(ProjectAdminQuery query, CancellationToken cancellationToken);
    Task<Project?> GetAsync(Guid id, bool tracked, CancellationToken cancellationToken);
    Task<bool> SlugExistsAsync(string slug, Guid? excludedId, CancellationToken cancellationToken);
    Task<bool> TechnologyIdsExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task AddAsync(Project project, CancellationToken cancellationToken);
    void Remove(Project project);
    Task<ProjectReorderResult> ReorderAsync(IReadOnlyList<ProjectOrderItem> items, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
