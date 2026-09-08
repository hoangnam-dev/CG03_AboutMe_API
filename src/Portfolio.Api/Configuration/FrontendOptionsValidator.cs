using Microsoft.Extensions.Options;

namespace Portfolio.Api.Configuration;

public sealed class FrontendOptionsValidator : IValidateOptions<FrontendOptions>
{
    public ValidateOptionsResult Validate(string? name, FrontendOptions options)
    {
        if (options.Origins is null || options.Origins.Length == 0)
        {
            return ValidateOptionsResult.Fail(
                "Frontend:Origins must contain at least one trusted origin.");
        }

        foreach (var configuredOrigin in options.Origins)
        {
            if (string.IsNullOrWhiteSpace(configuredOrigin) ||
                configuredOrigin != configuredOrigin.Trim() ||
                configuredOrigin.EndsWith('/') ||
                !Uri.TryCreate(configuredOrigin, UriKind.Absolute, out var origin) ||
                (origin.Scheme != Uri.UriSchemeHttps && origin.Scheme != Uri.UriSchemeHttp) ||
                origin.PathAndQuery != "/" ||
                !string.IsNullOrEmpty(origin.Fragment) ||
                !string.IsNullOrEmpty(origin.UserInfo))
            {
                return ValidateOptionsResult.Fail(
                    $"Frontend:Origins contains invalid origin '{configuredOrigin}'. " +
                    "Each value must be an absolute HTTP(S) origin without credentials, path, query, fragment, whitespace, or a trailing slash.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
