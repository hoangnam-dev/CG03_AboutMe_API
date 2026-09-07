namespace Portfolio.Infrastructure.Authentication;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    public int IdleLifetimeDays { get; set; } = 7;
    public int AbsoluteLifetimeDays { get; set; } = 30;
    public string Pepper { get; set; } = string.Empty;
    public string CookieName { get; set; } = "__Host-refresh";
    public string CsrfCookieName { get; set; } = "__Host-csrf";
    public string CookieSameSite { get; set; } = "Lax";
}
