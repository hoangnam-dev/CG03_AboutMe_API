using Microsoft.Extensions.Options;

namespace Portfolio.Api.Configuration;

public sealed class FrontendOptionsValidator : IValidateOptions<FrontendOptions>
{
    public ValidateOptionsResult Validate(string? name, FrontendOptions options)
    {
        if (!Uri.TryCreate(options.Origin, UriKind.Absolute, out var origin) ||
            (origin.Scheme != Uri.UriSchemeHttps && origin.Scheme != Uri.UriSchemeHttp) ||
            origin.PathAndQuery != "/")
        {
            return ValidateOptionsResult.Fail(
                "Frontend:Origin must be an absolute HTTP(S) origin without a path.");
        }

        return ValidateOptionsResult.Success;
    }
}
