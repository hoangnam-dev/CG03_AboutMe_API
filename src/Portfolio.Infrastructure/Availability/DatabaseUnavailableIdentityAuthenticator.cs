using Portfolio.Application.Common.Authentication;
using Portfolio.Application.Common.Exceptions;

namespace Portfolio.Infrastructure.Availability;

internal sealed class DatabaseUnavailableIdentityAuthenticator : IIdentityAuthenticator
{
    public Task<AuthenticatedUser?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken) =>
        throw new ServiceUnavailableException(
            "Authentication is unavailable because PostgreSQL is not configured.");
}
