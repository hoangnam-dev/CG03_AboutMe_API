using Portfolio.Application.Common.Models;

namespace Portfolio.Application.Experiences;

public interface IExperienceRepository
{
    Task<IReadOnlyList<PublicExperienceResponse>?> GetPublicAsync(
        string slug, string locale, CancellationToken cancellationToken);
    Task<ExperienceAdminPage> GetExperiencesAsync(
        ExperienceAdminQuery query, CancellationToken cancellationToken);
    Task<WorkExperience?> GetAsync(Guid id, bool tracked, CancellationToken cancellationToken);
    Task<bool> TechnologyIdsExistAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task AddAsync(WorkExperience experience, CancellationToken cancellationToken);
    void Remove(WorkExperience experience);
    Task<ExperienceReorderResult> ReorderAsync(
        IReadOnlyList<ExperienceOrderItem> items, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
