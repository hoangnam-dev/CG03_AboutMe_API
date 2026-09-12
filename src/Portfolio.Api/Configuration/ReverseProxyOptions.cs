namespace Portfolio.Api.Configuration;

public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    public string[] KnownProxies { get; set; } = [];
}
