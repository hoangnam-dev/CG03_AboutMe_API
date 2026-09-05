using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "not-an-email", password = "" },
            TestContext.Current.CancellationToken);
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
    }

    [Fact]
    public async Task SwaggerDocumentIsAvailableInDevelopment()
    {
        var response = await _client.GetAsync(
            "/swagger/v1/swagger.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] =
                    "Host=localhost;Database=portfolio_api_tests;Username=test;Password=test",
                ["Jwt:Issuer"] = "portfolio-tests",
                ["Jwt:Audience"] = "portfolio-client",
                ["Jwt:SigningKey"] =
                    "this-is-a-strong-test-signing-key-with-more-than-32-bytes",
                ["Jwt:AccessTokenMinutes"] = "60",
                ["Frontend:Origin"] = "https://portfolio.example",
                ["BootstrapAdmin:Enabled"] = "false",
            });
        });
    }
}

public sealed class DatabaseOptionalApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] = string.Empty,
                ["Jwt:Issuer"] = "portfolio-tests",
                ["Jwt:Audience"] = "portfolio-client",
                ["Jwt:SigningKey"] =
                    "this-is-a-strong-test-signing-key-with-more-than-32-bytes",
                ["Jwt:AccessTokenMinutes"] = "60",
                ["Frontend:Origin"] = "https://portfolio.example",
                ["BootstrapAdmin:Enabled"] = "false",
            });
        });
    }
}
