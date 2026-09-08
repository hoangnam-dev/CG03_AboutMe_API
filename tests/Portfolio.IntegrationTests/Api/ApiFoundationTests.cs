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

    [Fact]
    public async Task SwaggerEveryJsonRequestBodyHasContractExample()
    {
        var expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["POST /api/v1/auth/login"] = ["email", "password"],
            ["PUT /api/v1/admin/profile"] = ["slug", "fullName", "email", "phone", "showEmail", "showPhone", "availableForWork", "translations", "socialLinks"],
            ["PUT /api/v1/admin/about"] = ["yearsOfExperience", "projectCount", "technologyCount", "showYearsOfExperience", "showProjectCount", "showTechnologyCount", "showContactSection", "isPublished", "translations"],
            ["POST /api/v1/admin/skill-categories"] = ["displayOrder", "isPublished", "translations"],
            ["PUT /api/v1/admin/skill-categories/{id}"] = ["displayOrder", "isPublished", "translations"],
            ["POST /api/v1/admin/skills"] = ["categoryId", "level", "yearsOfExperience", "icon", "displayOrder", "isPublished", "translations"],
            ["PUT /api/v1/admin/skills/{id}"] = ["categoryId", "level", "yearsOfExperience", "icon", "displayOrder", "isPublished", "translations"],
            ["PATCH /api/v1/admin/skills/reorder"] = ["items"],
            ["POST /api/v1/admin/experiences"] = ["companyName", "employmentType", "companyUrl", "startDate", "endDate", "displayOrder", "isPublished", "translations", "highlights", "technologyIds"],
            ["PUT /api/v1/admin/experiences/{id}"] = ["companyName", "employmentType", "companyUrl", "startDate", "endDate", "displayOrder", "isPublished", "translations", "highlights", "technologyIds"],
            ["PATCH /api/v1/admin/experiences/reorder"] = ["items"],
            ["POST /api/v1/admin/projects"] = ["slug", "internalName", "kind", "disclosureLevel", "repositoryUrl", "demoUrl", "thumbnailImageId", "startDate", "endDate", "isFeatured", "displayOrder", "isPublished", "translations", "technologyIds", "highlights", "images"],
            ["PUT /api/v1/admin/projects/{id}"] = ["slug", "internalName", "kind", "disclosureLevel", "repositoryUrl", "demoUrl", "thumbnailImageId", "startDate", "endDate", "isFeatured", "displayOrder", "isPublished", "translations", "technologyIds", "highlights", "images"],
            ["PATCH /api/v1/admin/projects/{id}/publish"] = ["isPublished"],
            ["PATCH /api/v1/admin/projects/reorder"] = ["items"],
            ["POST /api/v1/admin/certificates"] = ["issuer", "issuedDate", "expirationDate", "credentialId", "showCredentialId", "credentialUrl", "displayOrder", "isPublished", "translations", "technologyIds"],
            ["PUT /api/v1/admin/certificates/{id}"] = ["issuer", "issuedDate", "expirationDate", "credentialId", "showCredentialId", "credentialUrl", "displayOrder", "isPublished", "translations", "technologyIds"],
            ["PATCH /api/v1/admin/cv/{id}/current"] = ["isCurrent"],
            ["PATCH /api/v1/admin/cv/{id}/publish"] = ["isPublished"],
        };
        var json = await _client.GetStringAsync(
            "/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var actual = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!operation.Value.TryGetProperty("requestBody", out var requestBody) ||
                    !requestBody.GetProperty("content").TryGetProperty("application/json", out var mediaType))
                    continue;
                actual[$"{operation.Name.ToUpperInvariant()} {path.Name}"] = mediaType.GetProperty("example");
            }

        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach (var pair in expected)
        {
            var example = actual[pair.Key];
            Assert.Equal(pair.Value.Order(), example.EnumerateObject().Select(property => property.Name).Order());
            AssertNoPlaceholderProperties(example);
            if (example.TryGetProperty("translations", out var translations))
                Assert.Equal(["en", "vi"], translations.EnumerateObject().Select(property => property.Name).Order());
        }
    }

    private static void AssertNoPlaceholderProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                Assert.DoesNotMatch("^additionalProp", property.Name);
                AssertNoPlaceholderProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) AssertNoPlaceholderProperties(item);
        }
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
                ["Frontend:Origins:0"] = "https://portfolio.example",
                ["Frontend:Origins:1"] = "http://localhost:3000",
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
                ["Frontend:Origins:0"] = "https://portfolio.example",
                ["Frontend:Origins:1"] = "http://localhost:3000",
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
