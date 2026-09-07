using Microsoft.Extensions.Options;
using Portfolio.Infrastructure.Authentication;

namespace Portfolio.Api.Authentication;

public sealed class AuthCookieWriter(IOptions<RefreshTokenOptions> options)
{
    private readonly RefreshTokenOptions _options = options.Value;

    public void Write(
        HttpResponse response,
        string refreshToken,
        string csrfToken,
        DateTimeOffset expiresAt)
    {
        response.Cookies.Append(
            _options.CookieName,
            refreshToken,
            CreateOptions(expiresAt, httpOnly: true));
        response.Cookies.Append(
            _options.CsrfCookieName,
            csrfToken,
            CreateOptions(expiresAt, httpOnly: false));
    }

    public void Delete(HttpResponse response)
    {
        response.Cookies.Delete(
            _options.CookieName,
            CreateOptions(DateTimeOffset.UnixEpoch, httpOnly: true));
        response.Cookies.Delete(
            _options.CsrfCookieName,
            CreateOptions(DateTimeOffset.UnixEpoch, httpOnly: false));
    }

    private CookieOptions CreateOptions(DateTimeOffset expiresAt, bool httpOnly) => new()
    {
        HttpOnly = httpOnly,
        Secure = true,
        SameSite = Enum.Parse<SameSiteMode>(_options.CookieSameSite, ignoreCase: true),
        Path = "/",
        IsEssential = true,
        Expires = expiresAt,
    };
}
