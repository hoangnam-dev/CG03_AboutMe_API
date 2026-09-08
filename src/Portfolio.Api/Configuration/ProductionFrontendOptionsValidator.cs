using Microsoft.Extensions.Options;

namespace Portfolio.Api.Configuration;

public sealed class ProductionFrontendOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<FrontendOptions>
{
    public ValidateOptionsResult Validate(string? name, FrontendOptions options)
    {
        if (environment.IsDevelopment())
        {
            return ValidateOptionsResult.Success;
        }

        if (options.Origins is not { Length: 1 })
        {
            return ValidateOptionsResult.Fail(
                "Production Frontend:Origins must contain exactly one trusted HTTPS origin.");
        }

        return Uri.TryCreate(options.Origins[0], UriKind.Absolute, out var origin) &&
               origin.Scheme == Uri.UriSchemeHttps
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "Production Frontend:Origins must contain exactly one trusted HTTPS origin.");
    }
}
