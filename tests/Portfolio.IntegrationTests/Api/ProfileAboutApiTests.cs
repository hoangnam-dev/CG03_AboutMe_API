using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Portfolio.Application.About;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Profiles;
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
    public async Task SwaggerAboutUpdateExampleUsesSupportedLocales()
    {
        var json = await _client.GetStringAsync(
            "/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var example = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/admin/about")
            .GetProperty("put")
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("example");
        var translations = example.GetProperty("translations");

        Assert.True(translations.TryGetProperty("en", out _));
        Assert.True(translations.TryGetProperty("vi", out _));
        Assert.False(translations.TryGetProperty("additionalProp1", out _));
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
    public async Task MissingPublishedAboutReturnsNotFoundWithoutStoppingServer()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAboutRepository>();
                services.AddScoped<IAboutRepository, MissingAboutRepository>();
            }));
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var missing = await client.GetAsync(
            "/api/v1/portfolio/nguyen-hoang-nam/about?locale=vi",
            TestContext.Current.CancellationToken);
        var problem = await missing.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        var health = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.Equal(
            "Published About content was not found.",
            problem.GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task HiddenProfileContactPropertiesAreAbsentFromPublicJson()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProfileRepository>();
                services.AddSingleton<IProfileRepository>(new PublicProfileRepository());
            }));
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(
            "/api/v1/portfolio/nam/profile?locale=en",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var data = document.RootElement.GetProperty("data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(data.TryGetProperty("email", out _));
        Assert.False(data.TryGetProperty("phone", out _));
        Assert.DoesNotContain("hidden@example.com", data.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("+84-000-000-000", data.GetRawText(), StringComparison.Ordinal);
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

    private sealed class MissingAboutRepository : IAboutRepository
    {
        public Task<AboutPublicProjection?> GetPublicAsync(
            string slug,
            string locale,
            CancellationToken cancellationToken) =>
            Task.FromResult<AboutPublicProjection?>(null);

        public Task<Portfolio.Application.Common.Models.About?> GetAdminAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Portfolio.Application.Common.Models.About?> GetForUpdateAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Guid?> GetProfileIdAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddAsync(
            Portfolio.Application.Common.Models.About about,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class PublicProfileRepository : IProfileRepository
    {
        public Task<ProfilePublicProjection?> GetPublicAsync(
            string slug,
            string locale,
            CancellationToken cancellationToken) =>
            Task.FromResult<ProfilePublicProjection?>(new(
                slug,
                "Fictional Owner",
                "Engineer",
                "Public biography",
                "Ho Chi Minh City",
                null,
                true,
                "hidden@example.com",
                "+84-000-000-000",
                false,
                false,
                null,
                null,
                []));

        public Task<Profile?> GetAdminAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Profile?> GetForUpdateAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> SlugExistsAsync(
            string slug,
            Guid? excludedId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task AddAsync(Profile profile, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
