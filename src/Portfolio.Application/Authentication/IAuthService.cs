namespace Portfolio.Application.Authentication;

public interface IAuthService
{
    Task<AuthenticationResult> LoginAsync(
        LoginRequest request,
        AuthClientContext client,
        CancellationToken cancellationToken);
    Task<AuthenticationResult> RefreshAsync(
        string refreshToken,
        string csrfToken,
        AuthClientContext client,
        CancellationToken cancellationToken);
    Task LogoutAsync(string csrfToken, CancellationToken cancellationToken);
    Task LogoutAllAsync(string csrfToken, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuthSessionResponse>> GetSessionsAsync(CancellationToken cancellationToken);
    Task RevokeSessionAsync(
        Guid sessionId,
        string csrfToken,
        CancellationToken cancellationToken);
}
