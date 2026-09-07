using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Portfolio.Application.Common.Authentication;
using Portfolio.IntegrationTests.Infrastructure;
using Xunit;

namespace Portfolio.IntegrationTests.Api;

public sealed class ApiFoundationTests : IClassFixture<PortfolioApiFactory>
{
    private readonly HttpClient _client;

    public ApiFoundationTests(PortfolioApiFactory factory)
    {
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task HealthEndpointReturnsHealthy()
    {
        var response = await _client.GetAsync(
            "/health",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminDashboardRejectsAnonymousRequests()
    {
        var response = await _client.GetAsync(
            "/api/v1/admin/dashboard",
            TestContext.Current.CancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(401, payload.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("requestId").GetString()));
    }

    [Fact]
    public async Task LoginValidationReturnsProblemDetailsWithRequestId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "not-an-email", password = "" }),
        };
        request.Headers.Add("Origin", "https://portfolio.example");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(payload.TryGetProperty("errors", out _));
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("requestId").GetString()));
    }

    [Fact]
    public async Task CorsPreflightAllowsConfiguredFrontendOrigin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", "https://portfolio.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            "https://portfolio.example",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal(
            "true",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
    }

    [Fact]
    public async Task SwaggerDocumentIsAvailableInDevelopment()
    {
        var response = await _client.GetAsync(
            "/swagger/v1/swagger.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerDeclaresBearerAuthenticationForProtectedOperationsOnly()
    {
        var json = await _client.GetStringAsync(
            "/swagger/v1/swagger.json",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var bearer = root.GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");

        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearer.GetProperty("bearerFormat").GetString());

        var paths = root.GetProperty("paths");
        var adminSecurity = root.GetProperty("security");
        Assert.Contains(
            adminSecurity.EnumerateArray(),
            requirement => requirement.TryGetProperty("Bearer", out _));
        Assert.Empty(paths.GetProperty("/api/v1/auth/login")
            .GetProperty("post")
            .GetProperty("security")
            .EnumerateArray());
        Assert.Empty(paths.GetProperty("/api/v1/portfolio/{slug}/profile")
            .GetProperty("get")
            .GetProperty("security")
            .EnumerateArray());
    }
}

public sealed class DatabaseOptionalStartupTests
{
    [Fact]
    public async Task ApiStartsWithoutDatabaseConfiguration()
    {
        await using var factory = new DatabaseOptionalApiFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var liveness = await client.GetAsync(
            "/health",
            TestContext.Current.CancellationToken);
        var readiness = await client.GetAsync(
            "/health/ready",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
    }
}

public sealed class PortfolioApiFactory : WebApplicationFactory<Program>
{
    private readonly TestJwtCertificate _certificate = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting(
            "ConnectionStrings:PostgreSql",
            "Host=localhost;Database=portfolio_api_tests;Username=test;Password=test");
        builder.UseSetting("SupabaseStorage:Url", string.Empty);
        builder.UseSetting("SupabaseStorage:ServiceRoleKey", string.Empty);
        ApplyAuthenticationSettings(builder, _certificate);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] =
                    "Host=localhost;Database=portfolio_api_tests;Username=test;Password=test",
                ["Jwt:Issuer"] = "portfolio-tests",
                ["Jwt:Audience"] = "portfolio-client",
                ["Frontend:Origin"] = "https://portfolio.example",
                ["BootstrapAdmin:Enabled"] = "false",
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _certificate.Dispose();
        }
    }

    internal static void ApplyAuthenticationSettings(
        IWebHostBuilder builder,
        TestJwtCertificate certificate)
    {
        builder.UseSetting("Jwt:Issuer", "portfolio-tests");
        builder.UseSetting("Jwt:Audience", "portfolio-client");
        builder.UseSetting("Jwt:ActiveKeyId", certificate.KeyId);
        builder.UseSetting("Jwt:SigningCertificatePath", certificate.Path);
        builder.UseSetting("Jwt:SigningCertificatePassword", certificate.CertificatePassword);
        builder.UseSetting("Jwt:AccessTokenMinutes", "10");
        builder.UseSetting("Jwt:ClockSkewSeconds", "30");
        builder.UseSetting("RefreshToken:Pepper", "integration-test-pepper-with-at-least-32-bytes");
        builder.UseSetting("RefreshToken:CookieName", "__Host-refresh");
        builder.UseSetting("RefreshToken:CookieSameSite", "Lax");
    }
}

public class DatabaseOptionalApiFactory : WebApplicationFactory<Program>
{
    private readonly TestJwtCertificate _certificate = new();

    public string CreateToken(
        string role = "User",
        string issuer = "portfolio-tests",
        string audience = "portfolio-client",
        DateTime? expires = null) =>
        _certificate.Issue(Guid.NewGuid(), Guid.NewGuid(), 0, role, issuer, audience, expires);

    public string CreateTokenWithClaims(IEnumerable<System.Security.Claims.Claim> claims) =>
        _certificate.IssueClaims(claims);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:PostgreSql", string.Empty);
        builder.UseSetting("SupabaseStorage:Url", string.Empty);
        builder.UseSetting("SupabaseStorage:ServiceRoleKey", string.Empty);
        PortfolioApiFactory.ApplyAuthenticationSettings(builder, _certificate);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAccessSessionValidator>();
            services.AddScoped<IAccessSessionValidator, AlwaysValidAccessSessionValidator>();
        });
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] = string.Empty,
                ["Jwt:Issuer"] = "portfolio-tests",
                ["Jwt:Audience"] = "portfolio-client",
                ["Frontend:Origin"] = "https://portfolio.example",
                ["BootstrapAdmin:Enabled"] = "false",
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _certificate.Dispose();
        }
    }

    private sealed class AlwaysValidAccessSessionValidator : IAccessSessionValidator
    {
        public Task<bool> IsValidAsync(CurrentAuthSession current, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }
}
