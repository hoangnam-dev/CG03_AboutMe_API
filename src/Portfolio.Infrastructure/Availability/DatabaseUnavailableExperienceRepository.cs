using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Experiences;

namespace Portfolio.Infrastructure.Availability;

internal sealed class DatabaseUnavailableExperienceRepository : IExperienceRepository
{
    public Task<IReadOnlyList<PublicExperienceResponse>?> GetPublicAsync(string slug, string locale, CancellationToken token) => Fail<IReadOnlyList<PublicExperienceResponse>?>();
    public Task<ExperienceAdminPage> GetExperiencesAsync(ExperienceAdminQuery query, CancellationToken token) => Fail<ExperienceAdminPage>();
    public Task<WorkExperience?> GetAsync(Guid id, bool tracked, CancellationToken token) => Fail<WorkExperience?>();
    public Task<bool> TechnologyIdsExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => Fail<bool>();
    public Task AddAsync(WorkExperience experience, CancellationToken token) => Fail();
    public void Remove(WorkExperience experience) => throw Unavailable();
    public Task<ExperienceReorderResult> ReorderAsync(IReadOnlyList<ExperienceOrderItem> items, CancellationToken token) => Fail<ExperienceReorderResult>();
    public Task SaveChangesAsync(CancellationToken token) => Fail();

    private static Task<T> Fail<T>() => Task.FromException<T>(Unavailable());
    private static Task Fail() => Task.FromException(Unavailable());
    private static ServiceUnavailableException Unavailable() =>
        new("Database is temporarily unavailable.");
}
