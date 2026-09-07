using Portfolio.Application.Common.Models;

namespace Portfolio.Application.Authentication;

public interface IAuthSessionRepository
{
    Task CreateAsync(AuthSession session, RefreshToken refreshToken, CancellationToken cancellationToken);
    Task<Guid?> GetSessionIdForTokenAsync(Guid tokenId, CancellationToken cancellationToken);
    Task<RefreshRotationResult> RotateAsync(RefreshRotationCommand command, CancellationToken cancellationToken);
    Task RevokeAsync(Guid userId, Guid sessionId, DateTimeOffset revokedAt, string reason, CancellationToken cancellationToken);
    Task RevokeAllAsync(Guid userId, DateTimeOffset revokedAt, string reason, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuthSession>> GetByUserAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
