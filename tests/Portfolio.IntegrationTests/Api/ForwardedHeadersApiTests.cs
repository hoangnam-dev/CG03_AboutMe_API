using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Portfolio.IntegrationTests.Api;

public sealed class ForwardedHeadersApiTests
{
    [Fact]
    public async Task TrustedProxySuppliesClientIpAndHttpsScheme()
    {
        await using var factory = CreateFactory();

        var context = await factory.Server.SendAsync(requestContext =>
        {
            requestContext.Connection.RemoteIpAddress = IPAddress.Parse("172.30.0.1");
            requestContext.Request.Method = HttpMethods.Get;
            requestContext.Request.Path = "/health";
            requestContext.Request.Scheme = "http";
            requestContext.Request.Host = new HostString("api.example.com");
            requestContext.Request.Headers["X-Forwarded-For"] = "198.51.100.25";
            requestContext.Request.Headers["X-Forwarded-Proto"] = "https";
        }, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("https", context.Request.Scheme);
        Assert.Equal(IPAddress.Parse("198.51.100.25"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task UntrustedPeerCannotSupplyClientIpOrScheme()
    {
        await using var factory = CreateFactory();

        var context = await factory.Server.SendAsync(requestContext =>
        {
            requestContext.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
            requestContext.Request.Method = HttpMethods.Get;
            requestContext.Request.Path = "/health";
            requestContext.Request.Scheme = "http";
            requestContext.Request.Host = new HostString("api.example.com");
            requestContext.Request.Headers["X-Forwarded-For"] = "198.51.100.25";
            requestContext.Request.Headers["X-Forwarded-Proto"] = "https";
        }, TestContext.Current.CancellationToken);

        Assert.Equal("http", context.Request.Scheme);
        Assert.Equal(IPAddress.Parse("192.0.2.10"), context.Connection.RemoteIpAddress);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new DatabaseOptionalApiFactory().WithWebHostBuilder(builder =>
            builder.UseSetting("ReverseProxy:KnownProxies:0", "172.30.0.1"));
}
