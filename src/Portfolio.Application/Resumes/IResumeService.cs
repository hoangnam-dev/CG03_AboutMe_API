namespace Portfolio.Application.Resumes;

public interface IResumeService
{
    Task<ResumePublicResponse> GetPublicCurrentAsync(
        string slug,
        string? language,
        CancellationToken cancellationToken);

    Task<ResumeAdminPage> GetResumesAsync(
        ResumeAdminQuery query,
        CancellationToken cancellationToken);

    Task<ResumeAdminResponse> UploadAsync(
        ResumeUploadRequest request,
        CancellationToken cancellationToken);

    Task<ResumeAdminResponse> SetCurrentAsync(
        Guid id,
        ResumeCurrentRequest request,
        CancellationToken cancellationToken);

    Task<ResumeAdminResponse> SetPublishedAsync(
        Guid id,
        ResumePublishRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
