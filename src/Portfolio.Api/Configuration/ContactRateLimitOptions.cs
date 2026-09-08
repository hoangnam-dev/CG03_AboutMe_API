using Microsoft.Extensions.Options;

namespace Portfolio.Api.Configuration;

public sealed class ContactRateLimitOptions
{
    public const string SectionName = "RateLimit:Contact";

    public int PermitLimit { get; set; } = 5;
    public int WindowSeconds { get; set; } = 60;
}

public sealed class ContactRateLimitOptionsValidator : IValidateOptions<ContactRateLimitOptions>
{
    public ValidateOptionsResult Validate(string? name, ContactRateLimitOptions options) =>
        options.PermitLimit is >= 1 and <= 100 &&
        options.WindowSeconds is >= 1 and <= 3600
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "The Contact rate limit must use a 1-100 permit limit and a 1-3600 second window.");
}
