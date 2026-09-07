namespace Portfolio.Application.Skills;

public interface ISkillService
{
    Task<IReadOnlyList<PublicSkillCategoryResponse>> GetPublicAsync(string slug, string? locale, CancellationToken cancellationToken);
    Task<IReadOnlyList<SkillCategoryAdminResponse>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<SkillCategoryAdminResponse> CreateCategoryAsync(SkillCategoryWriteRequest request, CancellationToken cancellationToken);
    Task<SkillCategoryAdminResponse> UpdateCategoryAsync(Guid id, SkillCategoryWriteRequest request, CancellationToken cancellationToken);
    Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken);
    Task<SkillAdminPage> GetSkillsAsync(SkillAdminQuery query, CancellationToken cancellationToken);
    Task<SkillAdminResponse> GetSkillAsync(Guid id, CancellationToken cancellationToken);
    Task<SkillAdminResponse> CreateSkillAsync(SkillWriteRequest request, CancellationToken cancellationToken);
    Task<SkillAdminResponse> UpdateSkillAsync(Guid id, SkillWriteRequest request, CancellationToken cancellationToken);
    Task DeleteSkillAsync(Guid id, CancellationToken cancellationToken);
    Task ReorderSkillsAsync(Guid categoryId, SkillReorderRequest request, CancellationToken cancellationToken);
    Task<SkillIconUploadResponse> ReplaceIconAsync(Guid id, SkillIconUpload upload, CancellationToken cancellationToken);
}
