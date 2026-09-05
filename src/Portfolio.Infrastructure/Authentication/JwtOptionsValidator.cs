using System.Text;
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

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            failures.Add("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
        }

        if (options.AccessTokenMinutes is < 1 or > 1440)
        {
            failures.Add("Jwt:AccessTokenMinutes must be between 1 and 1440.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
