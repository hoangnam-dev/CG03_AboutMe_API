using Portfolio.Application.Common.Models;

namespace Portfolio.Application.Resumes;

public interface IResumeRepository
{
    Task<ResumePublicProjection?> GetPublicCurrentAsync(
        string slug,
        string language,
        CancellationToken cancellationToken);

    Task<ResumeEntityPage> GetResumesAsync(
        ResumeAdminQuery query,
        CancellationToken cancellationToken);

    Task<ResumeFile?> GetAsync(
        Guid id,
        bool tracked,
        CancellationToken cancellationToken);

    Task<ResumeFile> CreateVersionAsync(
        ResumeFile resumeFile,
        short versionYear,
        CancellationToken cancellationToken);

    Task<ResumeActivationResult> SetCurrentAsync(
        Guid id,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);

    void Remove(ResumeFile resumeFile);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
