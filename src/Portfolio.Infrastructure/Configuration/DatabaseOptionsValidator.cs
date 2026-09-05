using Microsoft.Extensions.Options;

namespace Portfolio.Infrastructure.Configuration;

public sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options) =>
        string.IsNullOrWhiteSpace(options.ConnectionString)
            ? ValidateOptionsResult.Fail("ConnectionStrings:PostgreSql is required.")
            : ValidateOptionsResult.Success;
}
