namespace Portfolio.Application.Experiences;

public interface IExperienceService
{
    Task<IReadOnlyList<PublicExperienceResponse>> GetPublicAsync(
        string slug, string? locale, CancellationToken cancellationToken);
    Task<ExperienceAdminPage> GetExperiencesAsync(
        ExperienceAdminQuery query, CancellationToken cancellationToken);
    Task<ExperienceAdminResponse> GetExperienceAsync(Guid id, CancellationToken cancellationToken);
    Task<ExperienceAdminResponse> CreateExperienceAsync(
        ExperienceWriteRequest request, CancellationToken cancellationToken);
    Task<ExperienceAdminResponse> UpdateExperienceAsync(
        Guid id, ExperienceWriteRequest request, CancellationToken cancellationToken);
    Task DeleteExperienceAsync(Guid id, CancellationToken cancellationToken);
    Task ReorderExperiencesAsync(
        ExperienceReorderRequest request, CancellationToken cancellationToken);
}
