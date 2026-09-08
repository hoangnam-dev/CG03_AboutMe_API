using Microsoft.Extensions.Options;

namespace Portfolio.Infrastructure.Authentication;

public sealed class ProductionBootstrapAdminOptionsValidator(bool productionValidationEnabled)
    : IValidateOptions<BootstrapAdminOptions>
{
    public ValidateOptionsResult Validate(string? name, BootstrapAdminOptions options) =>
        productionValidationEnabled && options.Enabled
            ? ValidateOptionsResult.Fail(
                "Bootstrap administration must be disabled outside Development.")
            : ValidateOptionsResult.Success;
}
