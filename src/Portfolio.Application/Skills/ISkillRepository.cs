using Portfolio.Application.Common.Models;

namespace Portfolio.Application.Skills;

public interface ISkillRepository
{
    Task<IReadOnlyList<PublicSkillCategoryResponse>?> GetPublicAsync(string slug, string locale, CancellationToken cancellationToken);
    Task<IReadOnlyList<SkillCategoryAdminResponse>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<SkillCategory?> GetCategoryForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> CategoryNameExistsAsync(Guid categoryId, string locale, string name, Guid? excludedId, CancellationToken cancellationToken);
    Task AddCategoryAsync(SkillCategory category, CancellationToken cancellationToken);
    Task<bool> CategoryHasSkillsAsync(Guid id, CancellationToken cancellationToken);
    void RemoveCategory(SkillCategory category);
    Task<SkillAdminPage> GetSkillsAsync(SkillAdminQuery query, CancellationToken cancellationToken);
    Task<Technology?> GetSkillAsync(Guid id, bool tracked, CancellationToken cancellationToken);
    Task<bool> SkillNameExistsAsync(Guid categoryId, string locale, string name, Guid? excludedId, CancellationToken cancellationToken);
    Task AddSkillAsync(Technology skill, CancellationToken cancellationToken);
    Task<bool> SkillHasReferencesAsync(Guid id, CancellationToken cancellationToken);
    void RemoveSkill(Technology skill);
    Task<SkillReorderResult> ReorderSkillsAsync(Guid categoryId, IReadOnlyList<SkillOrderItem> items, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
