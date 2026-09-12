using System.Net;
using Microsoft.Extensions.Options;

namespace Portfolio.Api.Configuration;

public sealed class ReverseProxyOptionsValidator(bool productionValidationEnabled)
    : IValidateOptions<ReverseProxyOptions>
{
    public ValidateOptionsResult Validate(string? name, ReverseProxyOptions options)
    {
        if (!productionValidationEnabled)
        {
            return ValidateOptionsResult.Success;
        }

        var configuredProxies = options.KnownProxies
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (configuredProxies.Length == 0)
        {
            return ValidateOptionsResult.Fail(
                "ReverseProxy:KnownProxies must contain at least one trusted proxy in Production.");
        }

        foreach (var configuredProxy in configuredProxies)
        {
            if (configuredProxy != configuredProxy.Trim() ||
                !IPAddress.TryParse(configuredProxy, out var address))
            {
                return ValidateOptionsResult.Fail(
                    "Every ReverseProxy:KnownProxies entry must be a literal IP address without whitespace.");
            }

        }

        return ValidateOptionsResult.Success;
    }
}
