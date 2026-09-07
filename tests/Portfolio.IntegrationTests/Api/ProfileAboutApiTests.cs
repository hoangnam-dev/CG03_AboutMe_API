using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Portfolio.IntegrationTests.Api;

public sealed class ProfileAboutApiTests : IClassFixture<DatabaseOptionalApiFactory>
{
    private readonly HttpClient _client;
    private readonly DatabaseOptionalApiFactory _factory;

    public ProfileAboutApiTests(DatabaseOptionalApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Theory]
    [InlineData("/api/v1/admin/profile")]
    [InlineData("/api/v1/admin/about")]
    public async Task AdminRoutesRejectAnonymousRequests(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SwaggerContainsEveryProfileAndAboutRoute()
    {
        var json = await _client.GetStringAsync(
            "/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/portfolio/{slug}/profile", out _));
        Assert.True(paths.TryGetProperty("/api/v1/portfolio/{slug}/about", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/profile", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/profile/avatar", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/profile/hero-image", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/about", out _));
    }

    [Fact]
    public async Task UnsupportedLocaleReturnsProblemDetailsBeforeDatabaseAccess()
    {
        var response = await _client.GetAsync(
            "/api/v1/portfolio/nam/profile?locale=fr",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task NonAdminTokenReceivesForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _factory.CreateToken());

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
