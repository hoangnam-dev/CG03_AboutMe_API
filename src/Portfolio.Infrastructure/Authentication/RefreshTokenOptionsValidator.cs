using System.Text;
using Microsoft.Extensions.Options;

namespace Portfolio.Infrastructure.Authentication;

public sealed class RefreshTokenOptionsValidator : IValidateOptions<RefreshTokenOptions>
{
    public ValidateOptionsResult Validate(string? name, RefreshTokenOptions options)
    {
        var failures = new List<string>();
        if (options.IdleLifetimeDays is < 1 or > 30)
        {
            failures.Add("RefreshToken:IdleLifetimeDays must be between 1 and 30.");
        }

        if (options.AbsoluteLifetimeDays is < 1 or > 90 ||
            options.AbsoluteLifetimeDays < options.IdleLifetimeDays)
        {
            failures.Add("RefreshToken:AbsoluteLifetimeDays must be between the idle lifetime and 90.");
        }

        if (Encoding.UTF8.GetByteCount(options.Pepper) < 32)
        {
            failures.Add("RefreshToken:Pepper must contain at least 32 UTF-8 bytes.");
        }

        if (!string.Equals(options.CookieName, "__Host-refresh", StringComparison.Ordinal))
        {
            failures.Add("RefreshToken:CookieName must be '__Host-refresh'.");
        }

        if (!string.Equals(options.CsrfCookieName, "__Host-csrf", StringComparison.Ordinal))
        {
            failures.Add("RefreshToken:CsrfCookieName must be '__Host-csrf'.");
        }

        if (!string.Equals(options.CookieSameSite, "Lax", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(options.CookieSameSite, "Strict", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(options.CookieSameSite, "None", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("RefreshToken:CookieSameSite must be Lax, Strict, or None.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
