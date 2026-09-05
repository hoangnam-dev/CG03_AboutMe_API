using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Portfolio.Infrastructure.Authentication;

public sealed class BootstrapAdminOptionsValidator : IValidateOptions<BootstrapAdminOptions>
{
    public ValidateOptionsResult Validate(string? name, BootstrapAdminOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Email) ||
            !new EmailAddressAttribute().IsValid(options.Email))
        {
            failures.Add("BootstrapAdmin:Email must be a valid email address when bootstrap is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            failures.Add("BootstrapAdmin:Password is required when bootstrap is enabled.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
