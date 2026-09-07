using Portfolio.Application.Authentication;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;

namespace Portfolio.Infrastructure.Availability;

internal sealed class DatabaseUnavailableAuthSessionRepository : IAuthSessionRepository
{
    public Task CreateAsync(AuthSession session, RefreshToken refreshToken, CancellationToken cancellationToken) =>
        throw Unavailable();

    public Task<Guid?> GetSessionIdForTokenAsync(Guid tokenId, CancellationToken cancellationToken) =>
        throw Unavailable();

    public Task<RefreshRotationResult> RotateAsync(RefreshRotationCommand command, CancellationToken cancellationToken) =>
        throw Unavailable();

    public Task RevokeAsync(Guid userId, Guid sessionId, DateTimeOffset revokedAt, string reason, CancellationToken cancellationToken) =>
        throw Unavailable();

    public Task RevokeAllAsync(Guid userId, DateTimeOffset revokedAt, string reason, CancellationToken cancellationToken) =>
        throw Unavailable();

    public Task<IReadOnlyList<AuthSession>> GetByUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        throw Unavailable();

    private static ServiceUnavailableException Unavailable() =>
        new("Authentication sessions are unavailable because PostgreSQL is not configured.");
}
