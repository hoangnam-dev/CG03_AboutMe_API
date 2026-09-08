using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Resumes;

namespace Portfolio.Infrastructure.Availability;

public sealed class DatabaseUnavailableResumeRepository : IResumeRepository
{
    private static ServiceUnavailableException Unavailable() => new("PostgreSQL is not configured.");

    public Task<ResumePublicProjection?> GetPublicCurrentAsync(string slug, string language, CancellationToken cancellationToken) => throw Unavailable();
    public Task<ResumeEntityPage> GetResumesAsync(ResumeAdminQuery query, CancellationToken cancellationToken) => throw Unavailable();
    public Task<ResumeFile?> GetAsync(Guid id, bool tracked, CancellationToken cancellationToken) => throw Unavailable();
    public Task<ResumeFile> CreateVersionAsync(ResumeFile resumeFile, short versionYear, CancellationToken cancellationToken) => throw Unavailable();
    public Task<ResumeActivationResult> SetCurrentAsync(Guid id, DateTimeOffset updatedAt, CancellationToken cancellationToken) => throw Unavailable();
    public void Remove(ResumeFile resumeFile) => throw Unavailable();
    public Task SaveChangesAsync(CancellationToken cancellationToken) => throw Unavailable();
}
