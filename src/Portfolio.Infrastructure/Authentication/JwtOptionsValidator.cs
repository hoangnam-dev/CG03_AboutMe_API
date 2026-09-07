using Microsoft.Extensions.Options;

namespace Portfolio.Infrastructure.Authentication;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ActiveKeyId))
        {
            failures.Add("Jwt:ActiveKeyId is required.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningCertificatePath))
        {
            failures.Add("Jwt:SigningCertificatePath is required.");
        }

        if (options.AccessTokenMinutes is < 1 or > 60)
        {
            failures.Add("Jwt:AccessTokenMinutes must be between 1 and 60.");
        }

        if (options.ClockSkewSeconds is < 0 or > 60)
        {
            failures.Add("Jwt:ClockSkewSeconds must be between 0 and 60.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
