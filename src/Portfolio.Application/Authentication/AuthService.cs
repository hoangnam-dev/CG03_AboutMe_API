using Portfolio.Application.Common.Authentication;
using Portfolio.Application.Common.Exceptions;

namespace Portfolio.Application.Authentication;

public sealed class AuthService(
    IIdentityAuthenticator identityAuthenticator,
    IAccessTokenIssuer accessTokenIssuer) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await identityAuthenticator.AuthenticateAsync(
            request.Email,
            request.Password,
            cancellationToken);

        if (user is null)
        {
            throw new AuthenticationFailedException();
        }

        var token = accessTokenIssuer.Issue(user);
        return new LoginResponse(token.Value, "Bearer", token.ExpiresAt);
    }
}
