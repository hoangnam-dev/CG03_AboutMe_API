using Microsoft.Extensions.Logging;
using Portfolio.Application.Common.Authentication;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;

namespace Portfolio.Application.Authentication;

public sealed partial class AuthService(
    IIdentityAuthenticator identityAuthenticator,
    IAccessTokenIssuer accessTokenIssuer,
    IAuthSessionRepository sessionRepository,
    IRefreshTokenProtector refreshTokenProtector,
    ICsrfTokenService csrfTokenService,
    IAuthSessionLifetime sessionLifetime,
    ICurrentUserAccessor currentUserAccessor,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthenticationResult> LoginAsync(
        LoginRequest request,
        AuthClientContext client,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await identityAuthenticator.AuthenticateAsync(
            request.Email,
            request.Password,
            cancellationToken);
        if (user is null)
        {
            LogLoginFailed(logger);
            throw new AuthenticationFailedException();
        }

        var now = timeProvider.GetUtcNow();
        var sessionId = Guid.NewGuid();
        var generated = refreshTokenProtector.Generate(Guid.NewGuid());
        var absoluteExpiry = now.Add(sessionLifetime.AbsoluteLifetime);
        var refreshExpiry = Min(now.Add(sessionLifetime.IdleLifetime), absoluteExpiry);
        var session = new AuthSession
        {
            Id = sessionId,
            UserId = user.Id,
            CreatedAt = now,
            LastUsedAt = now,
            IdleExpiresAt = refreshExpiry,
            AbsoluteExpiresAt = absoluteExpiry,
            CreatedIp = NormalizeIp(client.IpAddress),
            LastIp = NormalizeIp(client.IpAddress),
            UserAgent = NormalizeUserAgent(client.UserAgent),
        };
        var refreshToken = new RefreshToken
        {
            Id = generated.TokenId,
            SessionId = sessionId,
            SecretHash = generated.SecretHash,
            CreatedAt = now,
            ExpiresAt = refreshExpiry,
            CreatedByIp = session.CreatedIp,
        };

        await sessionRepository.CreateAsync(session, refreshToken, cancellationToken);
        LogLoginSucceeded(logger, user.Id, sessionId);
        return CreateResult(user, sessionId, generated.Value, refreshExpiry);
    }

    public async Task<AuthenticationResult> RefreshAsync(
        string refreshToken,
        string csrfToken,
        AuthClientContext client,
        CancellationToken cancellationToken)
    {
        var parsed = refreshTokenProtector.ParseAndHash(refreshToken)
            ?? throw new AuthenticationFailedException();
        var sessionId = await sessionRepository.GetSessionIdForTokenAsync(
            parsed.TokenId,
            cancellationToken);
        if (sessionId is null)
        {
            throw new AuthenticationFailedException();
        }

        if (!csrfTokenService.Validate(sessionId.Value, csrfToken))
        {
            throw new ForbiddenException("The CSRF token is invalid.");
        }

        var replacement = refreshTokenProtector.Generate(Guid.NewGuid());
        var rotation = await sessionRepository.RotateAsync(
            new RefreshRotationCommand(
                parsed.TokenId,
                parsed.SecretHash,
                replacement.TokenId,
                replacement.SecretHash,
                timeProvider.GetUtcNow(),
                sessionLifetime.IdleLifetime,
                NormalizeIp(client.IpAddress)),
            cancellationToken);
        if (rotation.Status != RefreshRotationStatus.Succeeded || rotation.User is null)
        {
            if (rotation.Status == RefreshRotationStatus.Reused)
            {
                LogRefreshReuse(logger, rotation.SessionId);
            }

            throw new AuthenticationFailedException();
        }

        LogRefreshSucceeded(logger, rotation.User.Id, rotation.SessionId);
        return CreateResult(
            rotation.User,
            rotation.SessionId,
            replacement.Value,
            rotation.RefreshTokenExpiresAt);
    }

    public async Task LogoutAsync(string csrfToken, CancellationToken cancellationToken)
    {
        var current = RequireCsrf(csrfToken);
        await sessionRepository.RevokeAsync(
            current.UserId,
            current.SessionId,
            timeProvider.GetUtcNow(),
            "logout",
            cancellationToken);
        LogLogout(logger, current.UserId, current.SessionId);
    }

    public async Task LogoutAllAsync(string csrfToken, CancellationToken cancellationToken)
    {
        var current = RequireCsrf(csrfToken);
        await sessionRepository.RevokeAllAsync(
            current.UserId,
            timeProvider.GetUtcNow(),
            "logout-all",
            cancellationToken);
        LogLogoutAll(logger, current.UserId);
    }

    public async Task<IReadOnlyList<AuthSessionResponse>> GetSessionsAsync(
        CancellationToken cancellationToken)
    {
        var current = currentUserAccessor.GetRequired();
        var sessions = await sessionRepository.GetByUserAsync(
            current.UserId,
            timeProvider.GetUtcNow(),
            cancellationToken);
        return sessions.Select(session => new AuthSessionResponse(
                session.Id,
                session.CreatedAt,
                session.LastUsedAt,
                session.IdleExpiresAt,
                session.AbsoluteExpiresAt,
                session.CreatedIp,
                session.LastIp,
                session.UserAgent,
                session.Id == current.SessionId))
            .ToArray();
    }

    public async Task RevokeSessionAsync(
        Guid sessionId,
        string csrfToken,
        CancellationToken cancellationToken)
    {
        var current = RequireCsrf(csrfToken);
        await sessionRepository.RevokeAsync(
            current.UserId,
            sessionId,
            timeProvider.GetUtcNow(),
            "session-revoked",
            cancellationToken);
        LogSessionRevoked(logger, current.UserId, sessionId);
    }

    private AuthenticationResult CreateResult(
        AuthenticatedUser user,
        Guid sessionId,
        string refreshToken,
        DateTimeOffset refreshExpiry)
    {
        var accessToken = accessTokenIssuer.Issue(user, sessionId);
        return new AuthenticationResult(
            new LoginResponse(
                accessToken.Value,
                "Bearer",
                accessToken.ExpiresAt,
                csrfTokenService.Issue(sessionId)),
            refreshToken,
            refreshExpiry);
    }

    private CurrentAuthSession RequireCsrf(string csrfToken)
    {
        var current = currentUserAccessor.GetRequired();
        if (!csrfTokenService.Validate(current.SessionId, csrfToken))
        {
            throw new ForbiddenException("The CSRF token is invalid.");
        }

        return current;
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) =>
        left <= right ? left : right;

    private static string NormalizeIp(string value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim()[..Math.Min(value.Trim().Length, 64)];

    private static string? NormalizeUserAgent(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized)
            ? null
            : normalized[..Math.Min(normalized.Length, 512)];
    }

    [LoggerMessage(EventId = 1100, Level = LogLevel.Warning, Message = "Administrator login failed")]
    private static partial void LogLoginFailed(ILogger logger);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "Administrator {UserId} logged in with session {SessionId}")]
    private static partial void LogLoginSucceeded(ILogger logger, Guid userId, Guid sessionId);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Information, Message = "Refresh succeeded for user {UserId} and session {SessionId}")]
    private static partial void LogRefreshSucceeded(ILogger logger, Guid userId, Guid sessionId);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Warning, Message = "Refresh token reuse revoked session {SessionId}")]
    private static partial void LogRefreshReuse(ILogger logger, Guid sessionId);

    [LoggerMessage(EventId = 1104, Level = LogLevel.Information, Message = "User {UserId} logged out session {SessionId}")]
    private static partial void LogLogout(ILogger logger, Guid userId, Guid sessionId);

    [LoggerMessage(EventId = 1105, Level = LogLevel.Warning, Message = "User {UserId} logged out all sessions")]
    private static partial void LogLogoutAll(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1106, Level = LogLevel.Warning, Message = "User {UserId} revoked session {SessionId}")]
    private static partial void LogSessionRevoked(ILogger logger, Guid userId, Guid sessionId);
}
