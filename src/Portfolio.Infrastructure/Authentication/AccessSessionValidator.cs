using Microsoft.EntityFrameworkCore;
using Portfolio.Application.Common.Authentication;
using Portfolio.Infrastructure.Persistence;

namespace Portfolio.Infrastructure.Authentication;

public sealed class AccessSessionValidator(
    PortfolioDbContext context,
    TimeProvider timeProvider) : IAccessSessionValidator
{
    public Task<bool> IsValidAsync(
        CurrentAuthSession current,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return context.AuthSessions.AsNoTracking().AnyAsync(
            session => session.Id == current.SessionId &&
                       session.UserId == current.UserId &&
                       session.RevokedAt == null &&
                       session.IdleExpiresAt > now &&
                       session.AbsoluteExpiresAt > now &&
                       context.Users.Any(user =>
                           user.Id == current.UserId &&
                           !user.IsDisabled &&
                           user.AuthVersion == current.AuthVersion &&
                           (!user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd <= now)),
            cancellationToken);
    }
}
