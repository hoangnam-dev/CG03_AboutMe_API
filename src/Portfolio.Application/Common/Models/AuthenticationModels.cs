namespace Portfolio.Application.Common.Models;

public sealed class AuthSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
    public DateTimeOffset IdleExpiresAt { get; set; }
    public DateTimeOffset AbsoluteExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokeReason { get; set; }
    public string CreatedIp { get; set; } = string.Empty;
    public string LastIp { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public byte[] SecretHash { get; set; } = [];
    public Guid? ParentTokenId { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokeReason { get; set; }
    public string CreatedByIp { get; set; } = string.Empty;
    public AuthSession Session { get; set; } = null!;
}
