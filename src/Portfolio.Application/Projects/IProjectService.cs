namespace Portfolio.Application.Projects;

public interface IProjectService
{
    Task<IReadOnlyList<PublicProjectListItem>> GetPublicListAsync(string slug, string? locale, CancellationToken cancellationToken);
    Task<PublicProjectDetail> GetPublicDetailAsync(string slug, string projectSlug, string? locale, CancellationToken cancellationToken);
    Task<ProjectAdminPage> GetProjectsAsync(ProjectAdminQuery query, CancellationToken cancellationToken);
    Task<ProjectAdminResponse> GetProjectAsync(Guid id, CancellationToken cancellationToken);
    Task<ProjectAdminResponse> CreateProjectAsync(ProjectWriteRequest request, CancellationToken cancellationToken);
    Task<ProjectAdminResponse> UpdateProjectAsync(Guid id, ProjectWriteRequest request, CancellationToken cancellationToken);
    Task DeleteProjectAsync(Guid id, CancellationToken cancellationToken);
    Task<ProjectAdminResponse> SetPublishedAsync(Guid id, ProjectPublishRequest request, CancellationToken cancellationToken);
    Task ReorderProjectsAsync(ProjectReorderRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectImageMetadataResponse>> UploadGalleryAsync(
        Guid id,
        IReadOnlyList<ProjectGalleryUpload> files,
        IReadOnlyList<ProjectGalleryMetadata> metadata,
        CancellationToken cancellationToken);
}
