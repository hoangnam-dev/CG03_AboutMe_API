using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Skills;

namespace Portfolio.Infrastructure.Availability;

internal sealed class DatabaseUnavailableSkillRepository : ISkillRepository
{
    public Task<IReadOnlyList<PublicSkillCategoryResponse>?> GetPublicAsync(string slug, string locale, CancellationToken token) => Fail<IReadOnlyList<PublicSkillCategoryResponse>?>();
    public Task<IReadOnlyList<SkillCategoryAdminResponse>> GetCategoriesAsync(CancellationToken token) => Fail<IReadOnlyList<SkillCategoryAdminResponse>>();
    public Task<SkillCategory?> GetCategoryForUpdateAsync(Guid id, CancellationToken token) => Fail<SkillCategory?>();
    public Task<bool> CategoryNameExistsAsync(Guid categoryId, string locale, string name, Guid? excludedId, CancellationToken token) => Fail<bool>();
    public Task AddCategoryAsync(SkillCategory category, CancellationToken token) => Fail();
    public Task<bool> CategoryHasSkillsAsync(Guid id, CancellationToken token) => Fail<bool>();
    public void RemoveCategory(SkillCategory category) => throw Unavailable();
    public Task<SkillAdminPage> GetSkillsAsync(SkillAdminQuery query, CancellationToken token) => Fail<SkillAdminPage>();
    public Task<Technology?> GetSkillAsync(Guid id, bool tracked, CancellationToken token) => Fail<Technology?>();
    public Task<bool> SkillNameExistsAsync(Guid categoryId, string locale, string name, Guid? excludedId, CancellationToken token) => Fail<bool>();
    public Task AddSkillAsync(Technology skill, CancellationToken token) => Fail();
    public Task<bool> SkillHasReferencesAsync(Guid id, CancellationToken token) => Fail<bool>();
    public void RemoveSkill(Technology skill) => throw Unavailable();
    public Task<SkillReorderResult> ReorderSkillsAsync(Guid categoryId, IReadOnlyList<SkillOrderItem> items, CancellationToken token) => Fail<SkillReorderResult>();
    public Task SaveChangesAsync(CancellationToken token) => Fail();
    private static Task<T> Fail<T>() => Task.FromException<T>(Unavailable());
    private static Task Fail() => Task.FromException(Unavailable());
    private static ServiceUnavailableException Unavailable() => new("Database is temporarily unavailable.");
}
