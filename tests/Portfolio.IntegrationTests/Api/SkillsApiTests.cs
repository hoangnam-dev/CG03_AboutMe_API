using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Portfolio.IntegrationTests.Api;

public sealed class SkillsApiTests : IClassFixture<DatabaseOptionalApiFactory>
{
    private readonly DatabaseOptionalApiFactory _factory;
    private readonly HttpClient _client;

    public SkillsApiTests(DatabaseOptionalApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task SwaggerContainsEverySkillsRoute()
    {
        var json = await _client.GetStringAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/portfolio/{slug}/skills", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/skill-categories", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/skill-categories/{id}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/skills", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/skills/{id}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/skills/reorder", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/skills/{id}/icon", out _));
    }

    [Theory]
    [InlineData("/api/v1/admin/skill-categories")]
    [InlineData("/api/v1/admin/skills")]
    public async Task AdminSkillsRoutesRejectAnonymousRequests(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AdminSkillsRoutesRejectNonAdminToken()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/skills");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _factory.CreateToken());

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnsupportedSkillsLocaleReturnsProblemDetailsBeforeDatabaseAccess()
    {
        var response = await _client.GetAsync(
            "/api/v1/portfolio/nam/skills?locale=fr", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
