using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Portfolio.Application.Authentication;
using Portfolio.Application.Common.Exceptions;
using Xunit;

namespace Portfolio.IntegrationTests.Api;

public sealed class AuthApiTests
{
    [Fact]
    public async Task LoginReturnsNonCacheableAccessTokenAndSecureHostCookie()
    {
        await using var factory = new AuthContractApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        using var request = CreateJsonRequest(HttpMethod.Post, "/api/v1/auth/login", new
        {
            email = "admin@example.com",
            password = "valid-password",
        });

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        var refreshCookie = Assert.Single(cookies, value =>
            value.StartsWith("__Host-refresh=", StringComparison.Ordinal));
        var csrfCookie = Assert.Single(cookies, value =>
            value.StartsWith("__Host-csrf=", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-cache", response.Headers.Pragma.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("httponly", csrfCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", csrfCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", csrfCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", csrfCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("access-value", json, StringComparison.Ordinal);
        Assert.Contains("csrf-value", json, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-value", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginRejectsMissingOrUntrustedOrigin()
    {
        await using var factory = new AuthContractApiFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var missing = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "admin@example.com", password = "valid-password" }),
        };
        using var untrusted = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "admin@example.com", password = "valid-password" }),
        };
        untrusted.Headers.Add("Origin", "https://attacker.example");

        var missingResponse = await client.SendAsync(missing, TestContext.Current.CancellationToken);
        var untrustedResponse = await client.SendAsync(untrusted, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, untrustedResponse.StatusCode);
    }

    [Fact]
    public async Task OriginProtectionCannotBeBypassedWithRouteCasing()
    {
        await using var factory = new AuthContractApiFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/API/V1/AUTH/LOGIN")
        {
            Content = JsonContent.Create(new
            {
                email = "admin@example.com",
                password = "valid-password",
            }),
        };

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LoginIsRateLimitedByClientIp()
    {
        await using var factory = new AuthContractApiFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var responses = new List<HttpResponseMessage>();
        try
        {
            for (var attempt = 0; attempt < 6; attempt++)
            {
                using var request = CreateJsonRequest(HttpMethod.Post, "/api/v1/auth/login", new
                {
                    email = "admin@example.com",
                    password = "valid-password",
                });
                responses.Add(await client.SendAsync(request, TestContext.Current.CancellationToken));
            }

            Assert.All(responses.Take(5), response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            Assert.Equal(HttpStatusCode.TooManyRequests, responses[5].StatusCode);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task RefreshReadsCookieAndCsrfHeaderAndRotatesCookie()
    {
        await using var factory = new AuthContractApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Origin", "https://portfolio.example");
        request.Headers.Add("X-CSRF-Token", "csrf-value");
        request.Headers.Add("Cookie", "__Host-refresh=old-refresh-value");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("old-refresh-value", factory.Service.ObservedRefreshToken);
        Assert.Equal("csrf-value", factory.Service.ObservedCsrfToken);
        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(cookies, value => value.Contains(
            "__Host-refresh=refresh-value",
            StringComparison.Ordinal));
        Assert.Contains(cookies, value => value.Contains(
            "__Host-csrf=csrf-value",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task RejectedRefreshClearsCookieAndReturnsGenericProblemDetails()
    {
        await using var factory = new AuthContractApiFactory(rejectRefresh: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Origin", "https://portfolio.example");
        request.Headers.Add("X-CSRF-Token", "csrf-value");
        request.Headers.Add("Cookie", "__Host-refresh=reused-refresh-value");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(401, problem.GetProperty("status").GetInt32());
        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(cookies, value => value.StartsWith(
            "__Host-refresh=",
            StringComparison.Ordinal));
        Assert.Contains(cookies, value => value.StartsWith(
            "__Host-csrf=",
            StringComparison.Ordinal));
    }

    private static HttpRequestMessage CreateJsonRequest(HttpMethod method, string path, object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Origin", "https://portfolio.example");
        return request;
    }
}

public sealed class AuthContractApiFactory(bool rejectRefresh = false) : DatabaseOptionalApiFactory
{
    public StubAuthService Service { get; } = new(rejectRefresh);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthService>();
            services.AddSingleton<IAuthService>(Service);
        });
    }
}

public sealed class StubAuthService(bool rejectRefresh) : IAuthService
{
    private static readonly DateTimeOffset AccessExpiry =
        new(2026, 9, 6, 12, 10, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RefreshExpiry =
        new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    public string? ObservedRefreshToken { get; private set; }
    public string? ObservedCsrfToken { get; private set; }

    public Task<AuthenticationResult> LoginAsync(
        LoginRequest request,
        AuthClientContext client,
        CancellationToken cancellationToken) => Task.FromResult(Result());

    public Task<AuthenticationResult> RefreshAsync(
        string refreshToken,
        string csrfToken,
        AuthClientContext client,
        CancellationToken cancellationToken)
    {
        ObservedRefreshToken = refreshToken;
        ObservedCsrfToken = csrfToken;
        return rejectRefresh
            ? Task.FromException<AuthenticationResult>(new AuthenticationFailedException())
            : Task.FromResult(Result());
    }

    public Task LogoutAsync(string csrfToken, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task LogoutAllAsync(string csrfToken, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<IReadOnlyList<AuthSessionResponse>> GetSessionsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AuthSessionResponse>>([]);
    public Task RevokeSessionAsync(Guid sessionId, string csrfToken, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    private static AuthenticationResult Result() => new(
        new LoginResponse("access-value", "Bearer", AccessExpiry, "csrf-value"),
        "refresh-value",
        RefreshExpiry);
}
