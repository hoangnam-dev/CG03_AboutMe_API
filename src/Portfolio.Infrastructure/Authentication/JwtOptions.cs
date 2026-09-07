namespace Portfolio.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string ActiveKeyId { get; set; } = string.Empty;
    public string SigningCertificatePath { get; set; } = string.Empty;
    public string SigningCertificatePassword { get; set; } = string.Empty;
    public Dictionary<string, string> ValidationCertificatePaths { get; set; } = [];
    public int AccessTokenMinutes { get; set; } = 10;
    public int ClockSkewSeconds { get; set; } = 30;
}
