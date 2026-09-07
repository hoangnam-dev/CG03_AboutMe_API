using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using Portfolio.Application.Common.Authentication;
using Portfolio.Application.Common.Exceptions;

namespace Portfolio.Api.Authentication;

public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserAccessor
{
    public CurrentAuthSession GetRequired()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        var subject = principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var session = principal?.FindFirst("sid")?.Value;
        var version = principal?.FindFirst("auth_version")?.Value;
        if (!Guid.TryParse(subject, out var userId) ||
            !Guid.TryParse(session, out var sessionId) ||
            !int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out var authVersion))
        {
            throw new AuthenticationFailedException();
        }

        return new CurrentAuthSession(userId, sessionId, authVersion);
    }
}
