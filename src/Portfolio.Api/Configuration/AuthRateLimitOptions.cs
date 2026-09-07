using Microsoft.Extensions.Options;

namespace Portfolio.Api.Configuration;

public sealed class AuthRateLimitOptions
{
    public const string SectionName = "RateLimit:Auth";

    public int LoginPermitLimit { get; set; } = 5;
    public int RefreshPermitLimit { get; set; } = 30;
    public int WindowSeconds { get; set; } = 60;
}

public sealed class AuthRateLimitOptionsValidator : IValidateOptions<AuthRateLimitOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthRateLimitOptions options) =>
        options.LoginPermitLimit is >= 1 and <= 100 &&
        options.RefreshPermitLimit is >= 1 and <= 300 &&
        options.WindowSeconds is >= 1 and <= 3600
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "Auth rate limits must use positive bounded permit limits and a 1-3600 second window.");
}
