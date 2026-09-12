using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Portfolio.Api.Configuration;

public sealed class ConfigureForwardedHeadersOptions(
    IOptions<ReverseProxyOptions> configuredOptions)
    : IConfigureOptions<ForwardedHeadersOptions>
{
    public void Configure(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var configuredProxy in configuredOptions.Value.KnownProxies)
        {
            if (IPAddress.TryParse(configuredProxy, out var address))
            {
                options.KnownProxies.Add(address);
            }
        }
    }
}
