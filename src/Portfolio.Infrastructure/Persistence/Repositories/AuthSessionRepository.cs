using Microsoft.EntityFrameworkCore;
using Portfolio.Application.Authentication;
using Portfolio.Application.Common.Authentication;
using Portfolio.Application.Common.Models;

namespace Portfolio.Infrastructure.Persistence.Repositories;

public sealed class AuthSessionRepository(
    PortfolioDbContext context,
    IRefreshTokenProtector tokenProtector) : IAuthSessionRepository
{
    public async Task CreateAsync(
        AuthSession session,
        RefreshToken refreshToken,
        CancellationToken cancellationToken)
    {
        session.RefreshTokens.Add(refreshToken);
        await context.AuthSessions.AddAsync(session, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<Guid?> GetSessionIdForTokenAsync(
        Guid tokenId,
        CancellationToken cancellationToken) =>
        context.RefreshTokens.AsNoTracking()
            .Where(token => token.Id == tokenId)
            .Select(token => (Guid?)token.SessionId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<RefreshRotationResult> RotateAsync(
        RefreshRotationCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var token = await context.RefreshTokens
            .FromSqlInterpolated($"SELECT * FROM refresh_tokens WHERE id = {command.TokenId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (token is null ||
            !tokenProtector.FixedTimeEquals(token.SecretHash, command.CandidateSecretHash))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new RefreshRotationResult(RefreshRotationStatus.Invalid, Guid.Empty, null, default);
        }

        var session = await context.AuthSessions.SingleAsync(
            current => current.Id == token.SessionId,
            cancellationToken);
        if (token.ConsumedAt is not null || token.RevokedAt is not null)
        {
            await RevokeTrackedSessionAsync(session, command.Now, "refresh-token-reuse", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RefreshRotationResult(RefreshRotationStatus.Reused, session.Id, null, default);
        }

        var user = await context.Users.SingleOrDefaultAsync(
            current => current.Id == session.UserId,
            cancellationToken);
        var sessionInvalid = token.ExpiresAt <= command.Now ||
                             session.RevokedAt is not null ||
                             session.IdleExpiresAt <= command.Now ||
                             session.AbsoluteExpiresAt <= command.Now;
        var userInvalid = user is null || user.IsDisabled ||
                          user.LockoutEnabled && user.LockoutEnd > command.Now;
        if (sessionInvalid || userInvalid)
        {
            await RevokeTrackedSessionAsync(session, command.Now, "refresh-invalid", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RefreshRotationResult(RefreshRotationStatus.Invalid, session.Id, null, default);
        }

        token.ConsumedAt = command.Now;
        await context.SaveChangesAsync(cancellationToken);

        var replacementExpiry = Min(command.Now.Add(command.IdleLifetime), session.AbsoluteExpiresAt);
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = command.ReplacementTokenId,
            SessionId = session.Id,
            SecretHash = command.ReplacementSecretHash,
            ParentTokenId = token.Id,
            CreatedAt = command.Now,
            ExpiresAt = replacementExpiry,
            CreatedByIp = command.IpAddress,
        });
        session.LastUsedAt = command.Now;
        session.LastIp = command.IpAddress;
        session.IdleExpiresAt = replacementExpiry;
        await context.SaveChangesAsync(cancellationToken);
        token.ReplacedByTokenId = command.ReplacementTokenId;
        await context.SaveChangesAsync(cancellationToken);

        var roles = await context.UserRoles
            .Where(userRole => userRole.UserId == user!.Id)
            .Join(
                context.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (_, role) => role.Name!)
            .ToArrayAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RefreshRotationResult(
            RefreshRotationStatus.Succeeded,
            session.Id,
            new AuthenticatedUser(user!.Id, user.Email!, roles, user.AuthVersion),
            replacementExpiry);
    }

    public async Task RevokeAsync(
        Guid userId,
        Guid sessionId,
        DateTimeOffset revokedAt,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var session = await context.AuthSessions.SingleOrDefaultAsync(
            current => current.Id == sessionId && current.UserId == userId,
            cancellationToken);
        if (session is not null && session.RevokedAt is null)
        {
            await RevokeTrackedSessionAsync(session, revokedAt, reason, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RevokeAllAsync(
        Guid userId,
        DateTimeOffset revokedAt,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.AuthSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(session => session.RevokedAt, revokedAt)
                .SetProperty(session => session.RevokeReason, reason), cancellationToken);
        await context.RefreshTokens
            .Where(token => token.Session.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, revokedAt)
                .SetProperty(token => token.RevokeReason, reason), cancellationToken);
        await context.Users
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.AuthVersion, user => user.AuthVersion + 1), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuthSession>> GetByUserAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await context.AuthSessions.AsNoTracking()
            .Where(session => session.UserId == userId &&
                              session.RevokedAt == null &&
                              session.IdleExpiresAt > now &&
                              session.AbsoluteExpiresAt > now)
            .OrderByDescending(session => session.LastUsedAt)
            .ToArrayAsync(cancellationToken);

    private async Task RevokeTrackedSessionAsync(
        AuthSession session,
        DateTimeOffset revokedAt,
        string reason,
        CancellationToken cancellationToken)
    {
        session.RevokedAt ??= revokedAt;
        session.RevokeReason ??= reason;
        await context.RefreshTokens
            .Where(token => token.SessionId == session.Id && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, revokedAt)
                .SetProperty(token => token.RevokeReason, reason), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) =>
        left <= right ? left : right;
}
