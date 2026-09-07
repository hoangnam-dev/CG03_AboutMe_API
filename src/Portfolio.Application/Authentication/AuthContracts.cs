using Portfolio.Application.Common.Authentication;

namespace Portfolio.Application.Authentication;

public sealed record AuthClientContext(string IpAddress, string? UserAgent);

public sealed record AuthenticationResult(
    LoginResponse Response,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record AuthSessionResponse(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt,
    DateTimeOffset IdleExpiresAt,
    DateTimeOffset AbsoluteExpiresAt,
    string CreatedIp,
    string LastIp,
    string? UserAgent,
    bool IsCurrent);

public sealed record RefreshRotationCommand(
    Guid TokenId,
    byte[] CandidateSecretHash,
    Guid ReplacementTokenId,
    byte[] ReplacementSecretHash,
    DateTimeOffset Now,
    TimeSpan IdleLifetime,
    string IpAddress);

public enum RefreshRotationStatus
{
    Succeeded,
    Invalid,
    Reused,
}

public sealed record RefreshRotationResult(
    RefreshRotationStatus Status,
    Guid SessionId,
    AuthenticatedUser? User,
    DateTimeOffset RefreshTokenExpiresAt);
