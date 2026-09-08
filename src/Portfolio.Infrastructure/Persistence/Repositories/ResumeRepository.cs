using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Resumes;

namespace Portfolio.Infrastructure.Persistence.Repositories;

public sealed class ResumeRepository(PortfolioDbContext context) : IResumeRepository
{
    public async Task<ResumePublicProjection?> GetPublicCurrentAsync(
        string slug,
        string language,
        CancellationToken cancellationToken)
    {
        if (!await context.Profiles.AsNoTracking()
                .AnyAsync(profile => profile.Slug == slug, cancellationToken))
            return null;

        return await context.Resumes.AsNoTracking()
            .Where(item => item.LanguageCode == language && item.IsPublished && item.IsActive)
            .Select(item => new ResumePublicProjection(
                item.Id,
                item.LanguageCode,
                item.FileUrl,
                item.OriginalFileName,
                item.ContentType,
                item.FileSize,
                item.VersionYear,
                item.VersionSequence,
                item.Translations
                    .Where(translation => translation.LocaleCode == language)
                    .Select(translation => translation.Description)
                    .SingleOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ResumeEntityPage> GetResumesAsync(
        ResumeAdminQuery query,
        CancellationToken cancellationToken)
    {
        var source = context.Resumes.AsNoTracking().AsQueryable();
        if (query.Language is not null)
            source = source.Where(item => item.LanguageCode == query.Language);
        var total = await source.LongCountAsync(cancellationToken);
        var items = await source
            .OrderBy(item => item.LanguageCode)
            .ThenByDescending(item => item.VersionYear)
            .ThenByDescending(item => item.VersionSequence)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(item => item.Translations)
            .ToArrayAsync(cancellationToken);
        return new ResumeEntityPage(items, total, query.Page, query.PageSize);
    }

    public Task<ResumeFile?> GetAsync(
        Guid id,
        bool tracked,
        CancellationToken cancellationToken)
    {
        IQueryable<ResumeFile> query = context.Resumes.Include(item => item.Translations);
        if (!tracked) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<ResumeFile> CreateVersionAsync(
        ResumeFile resumeFile,
        short versionYear,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            resumeFile.VersionYear = versionYear;
            resumeFile.VersionSequence = await AllocateSequenceAsync(
                resumeFile.LanguageCode, versionYear, transaction, cancellationToken);
            if (resumeFile.IsActive)
            {
                await context.Resumes
                    .Where(item => item.LanguageCode == resumeFile.LanguageCode && item.IsActive)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(item => item.IsActive, false),
                        cancellationToken);
            }
            await context.Resumes.AddAsync(resumeFile, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return resumeFile;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<ResumeActivationResult> SetCurrentAsync(
        Guid id,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var target = await context.Resumes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (target is null)
            return new ResumeActivationResult(ResumeActivationStatus.NotFound, null);
        if (!target.IsPublished)
            return new ResumeActivationResult(ResumeActivationStatus.NotPublished, null);

        await context.Resumes
            .Where(item => item.LanguageCode == target.LanguageCode && item.IsActive)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.IsActive, false),
                cancellationToken);
        await context.Resumes
            .Where(item => item.Id == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.IsActive, true)
                    .SetProperty(item => item.UpdatedAt, updatedAt),
                cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        context.ChangeTracker.Clear();
        var activated = await GetAsync(id, false, cancellationToken);
        return new ResumeActivationResult(ResumeActivationStatus.Activated, activated);
    }

    public void Remove(ResumeFile resumeFile) => context.Resumes.Remove(resumeFile);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await context.SaveChangesAsync(cancellationToken);

    private async Task<int> AllocateSequenceAsync(
        string language,
        short versionYear,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        command.CommandText =
            """
            INSERT INTO resume_version_counters (language_code, version_year, last_sequence)
            VALUES (@language, @year, 1)
            ON CONFLICT (language_code, version_year)
            DO UPDATE SET
                last_sequence = resume_version_counters.last_sequence + 1,
                updated_at = now()
            RETURNING last_sequence;
            """;
        command.Parameters.AddWithValue("language", language);
        command.Parameters.AddWithValue("year", versionYear);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
