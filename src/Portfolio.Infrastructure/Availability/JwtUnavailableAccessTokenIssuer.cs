using Portfolio.Application.Common.Authentication;
using Portfolio.Application.Common.Exceptions;

namespace Portfolio.Infrastructure.Availability;

internal sealed class JwtUnavailableAccessTokenIssuer : IAccessTokenIssuer
{
    public AccessToken Issue(AuthenticatedUser user, Guid sessionId) =>
        throw new ServiceUnavailableException(
            "Token issuance is unavailable because JWT signing is not configured.");
}
