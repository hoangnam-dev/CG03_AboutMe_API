using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Projects;

namespace Portfolio.Infrastructure.Availability;

public sealed class DatabaseUnavailableProjectRepository : IProjectRepository
{
    private static ServiceUnavailableException Unavailable() =>
        new("PostgreSQL is not configured.");

    public Task<IReadOnlyList<ProjectPublicProjection>?> GetPublicListAsync(string slug, string locale, CancellationToken cancellationToken) => throw Unavailable();
    public Task<ProjectPublicProjection?> GetPublicDetailAsync(string slug, string projectSlug, string locale, CancellationToken cancellationToken) => throw Unavailable();
    public Task<ProjectEntityPage> GetProjectsAsync(ProjectAdminQuery query, CancellationToken cancellationToken) => throw Unavailable();
    public Task<Project?> GetAsync(Guid id, bool tracked, CancellationToken cancellationToken) => throw Unavailable();
    public Task<bool> SlugExistsAsync(string slug, Guid? excludedId, CancellationToken cancellationToken) => throw Unavailable();
    public Task<bool> TechnologyIdsExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => throw Unavailable();
    public Task AddAsync(Project project, CancellationToken cancellationToken) => throw Unavailable();
    public void Remove(Project project) => throw Unavailable();
    public Task<ProjectReorderResult> ReorderAsync(IReadOnlyList<ProjectOrderItem> items, CancellationToken cancellationToken) => throw Unavailable();
    public Task SaveChangesAsync(CancellationToken cancellationToken) => throw Unavailable();
}
