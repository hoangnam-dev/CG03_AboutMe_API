using System.Data;
using Microsoft.EntityFrameworkCore;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Skills;

namespace Portfolio.Infrastructure.Persistence.Repositories;

public sealed class SkillRepository(PortfolioDbContext context) : ISkillRepository
{
    public async Task<IReadOnlyList<PublicSkillCategoryResponse>?> GetPublicAsync(
        string slug, string locale, CancellationToken cancellationToken)
    {
        if (!await context.Profiles.AsNoTracking().AnyAsync(profile => profile.Slug == slug, cancellationToken))
            return null;

        var categories = await context.SkillCategories.AsNoTracking()
            .Where(category => category.IsPublished)
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Id)
            .Where(category => category.Translations.Any(item => item.LocaleCode == locale))
            .Include(category => category.Translations.Where(item => item.LocaleCode == locale))
            .Include(category => category.Technologies
                .Where(skill => skill.IsPublished)
                .OrderBy(skill => skill.DisplayOrder)
                .ThenBy(skill => skill.Id))
                .ThenInclude(skill => skill.Translations.Where(item => item.LocaleCode == locale))
            .AsSplitQuery()
            .ToArrayAsync(cancellationToken);
        return categories.Select(category => new PublicSkillCategoryResponse(
            category.Id,
            category.Translations.Single().Name,
            category.DisplayOrder,
            category.Technologies
                .Where(skill => skill.Translations.Count != 0)
                .OrderBy(skill => skill.DisplayOrder)
                .ThenBy(skill => skill.Id)
                .Select(skill => new PublicSkillResponse(
                    skill.Id,
                    skill.Translations.Single().Name,
                    skill.SkillLevel.ToString(),
                    skill.YearsOfExperience,
                    new SkillIconRequest(skill.IconType.ToString().ToLowerInvariant(), skill.IconValue),
                    skill.DisplayOrder))
                .ToArray()))
            .ToArray();
    }

    public async Task<IReadOnlyList<SkillCategoryAdminResponse>> GetCategoriesAsync(
        CancellationToken cancellationToken)
    {
        var categories = await context.SkillCategories.AsNoTracking()
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Id)
            .Include(category => category.Translations)
            .ToArrayAsync(cancellationToken);
        return categories.Select(category => new SkillCategoryAdminResponse(
            category.Id,
            category.DisplayOrder,
            category.IsPublished,
            category.Translations.ToDictionary(
                item => item.LocaleCode,
                item => new SkillTranslationRequest(item.Name),
                StringComparer.Ordinal)))
            .ToArray();
    }

    public Task<SkillCategory?> GetCategoryForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.SkillCategories.Include(category => category.Translations)
            .SingleOrDefaultAsync(category => category.Id == id, cancellationToken);

    public Task<bool> CategoryNameExistsAsync(
        Guid categoryId,
        string locale,
        string name,
        Guid? excludedId,
        CancellationToken cancellationToken) =>
        context.SkillCategoryTranslations.AsNoTracking().AnyAsync(
            item => item.LocaleCode == locale &&
                    EF.Functions.ILike(item.Name, name) &&
                    (!excludedId.HasValue || item.CategoryId != excludedId.Value),
            cancellationToken);

    public async Task AddCategoryAsync(SkillCategory category, CancellationToken cancellationToken) =>
        await context.SkillCategories.AddAsync(category, cancellationToken);

    public Task<bool> CategoryHasSkillsAsync(Guid id, CancellationToken cancellationToken) =>
        context.Technologies.AsNoTracking().AnyAsync(skill => skill.CategoryId == id, cancellationToken);

    public void RemoveCategory(SkillCategory category) => context.SkillCategories.Remove(category);

    public async Task<SkillAdminPage> GetSkillsAsync(
        SkillAdminQuery query, CancellationToken cancellationToken)
    {
        var source = context.Technologies.AsNoTracking().AsQueryable();
        if (query.CategoryId.HasValue)
            source = source.Where(skill => skill.CategoryId == query.CategoryId.Value);
        if (query.IsPublished.HasValue)
            source = source.Where(skill => skill.IsPublished == query.IsPublished.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
            source = source.Where(skill => skill.Translations.Any(
                translation => EF.Functions.ILike(translation.Name, $"%{query.Search}%")));

        var total = await source.LongCountAsync(cancellationToken);
        var skills = await source.OrderBy(skill => skill.DisplayOrder)
            .ThenBy(skill => skill.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(skill => skill.Translations)
            .ToArrayAsync(cancellationToken);
        return new SkillAdminPage(skills.Select(Map).ToArray(), total, query.Page, query.PageSize);
    }

    public Task<Technology?> GetSkillAsync(
        Guid id, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<Technology> query = context.Technologies.Include(skill => skill.Translations);
        if (!tracked) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(skill => skill.Id == id, cancellationToken);
    }

    public Task<bool> SkillNameExistsAsync(
        Guid categoryId,
        string locale,
        string name,
        Guid? excludedId,
        CancellationToken cancellationToken) =>
        context.TechnologyTranslations.AsNoTracking().AnyAsync(
            item => item.LocaleCode == locale &&
                    EF.Functions.ILike(item.Name, name) &&
                    (!excludedId.HasValue || item.TechnologyId != excludedId.Value),
            cancellationToken);

    public async Task AddSkillAsync(Technology skill, CancellationToken cancellationToken) =>
        await context.Technologies.AddAsync(skill, cancellationToken);

    public Task<bool> SkillHasReferencesAsync(Guid id, CancellationToken cancellationToken) =>
        context.Technologies.AsNoTracking().AnyAsync(
            skill => skill.Id == id &&
                     (skill.Experiences.Any() || skill.Projects.Any() || skill.Certificates.Any()),
            cancellationToken);

    public void RemoveSkill(Technology skill) => context.Technologies.Remove(skill);

    public async Task<SkillReorderResult> ReorderSkillsAsync(
        Guid categoryId,
        IReadOnlyList<SkillOrderItem> items,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        if (!await context.SkillCategories.AnyAsync(category => category.Id == categoryId, cancellationToken))
            return SkillReorderResult.CategoryNotFound;

        var skills = await context.Technologies
            .Where(skill => skill.CategoryId == categoryId)
            .ToArrayAsync(cancellationToken);
        var requestedIds = items.Select(item => item.Id).ToHashSet();
        if (skills.Length != requestedIds.Count || skills.Any(skill => !requestedIds.Contains(skill.Id)))
            return SkillReorderResult.SetMismatch;

        var orders = items.ToDictionary(item => item.Id, item => item.DisplayOrder);
        foreach (var skill in skills) skill.DisplayOrder = orders[skill.Id];
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SkillReorderResult.Success;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await context.SaveChangesAsync(cancellationToken);

    private static SkillAdminResponse Map(Technology skill) => new(
        skill.Id,
        skill.CategoryId,
        skill.SkillLevel.ToString(),
        skill.YearsOfExperience,
        new SkillIconRequest(skill.IconType.ToString().ToLowerInvariant(), skill.IconValue),
        skill.DisplayOrder,
        skill.IsPublished,
        skill.Translations.ToDictionary(
            item => item.LocaleCode,
            item => new SkillTranslationRequest(item.Name),
            StringComparer.Ordinal));
}
