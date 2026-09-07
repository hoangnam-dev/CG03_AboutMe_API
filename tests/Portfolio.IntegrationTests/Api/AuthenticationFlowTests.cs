using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Portfolio.Api.Models;
using Portfolio.Application.Authentication;
using Portfolio.Infrastructure.Authentication;
using Portfolio.Infrastructure.Persistence;
using Portfolio.IntegrationTests.Infrastructure;
using Xunit;

namespace Portfolio.IntegrationTests.Api;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class AuthenticationFlowTests(PostgreSqlFixture database)
{
    internal const string FrontendOrigin = "https://portfolio.example";
    private const string Password = "Test-password-123!";

    [Fact]
    public async Task LoginRotationAndReplayDetectionWorkEndToEnd()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var factory = new PostgreSqlAuthApiFactory(database.ConnectionString);
        var email = $"admin-{Guid.NewGuid():N}@example.com";
        await CreateAdminAsync(factory, email);
        using var client = CreateClient(factory);

        var login = await LoginAsync(client, email, Password);
        var loginBody = await login.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(
            TestContext.Current.CancellationToken);
        var originalRefresh = GetCookie(login, "__Host-refresh");
        var originalTokenId = Guid.Parse(originalRefresh[..originalRefresh.IndexOf('.')]);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.NotNull(loginBody);
        await using (var verification = database.CreateDbContext())
        {
            var stored = await verification.RefreshTokens.AsNoTracking()
                .SingleAsync(token => token.Id == originalTokenId, TestContext.Current.CancellationToken);
            Assert.Equal(32, stored.SecretHash.Length);
            Assert.DoesNotContain(
                Convert.ToBase64String(stored.SecretHash),
                originalRefresh,
                StringComparison.Ordinal);
        }

        using var refreshRequest = CreateRefreshRequest(originalRefresh, loginBody.Data.CsrfToken);
        var refresh = await client.SendAsync(refreshRequest, TestContext.Current.CancellationToken);
        var refreshBody = await refresh.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(
            TestContext.Current.CancellationToken);
        var replacementRefresh = GetCookie(refresh, "__Host-refresh");
        var replacementId = Guid.Parse(replacementRefresh[..replacementRefresh.IndexOf('.')]);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.NotNull(refreshBody);
        Assert.NotEqual(originalRefresh, replacementRefresh);
        await using (var verification = database.CreateDbContext())
        {
            var original = await verification.RefreshTokens.AsNoTracking()
                .SingleAsync(token => token.Id == originalTokenId, TestContext.Current.CancellationToken);
            var replacement = await verification.RefreshTokens.AsNoTracking()
                .SingleAsync(token => token.Id == replacementId, TestContext.Current.CancellationToken);
            Assert.NotNull(original.ConsumedAt);
            Assert.Equal(replacement.Id, original.ReplacedByTokenId);
            Assert.Equal(original.Id, replacement.ParentTokenId);
        }

        using var replayRequest = CreateRefreshRequest(originalRefresh, loginBody.Data.CsrfToken);
        var replay = await client.SendAsync(replayRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        await using (var verification = database.CreateDbContext())
        {
            var session = await verification.AuthSessions.AsNoTracking()
                .SingleAsync(TestContext.Current.CancellationToken);
            var tokens = await verification.RefreshTokens.AsNoTracking()
                .Where(token => token.SessionId == session.Id)
                .ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(session.RevokedAt);
            Assert.All(tokens, token => Assert.NotNull(token.RevokedAt));
        }
    }

    [Fact]
    public async Task LoginUsesGenericFailureForMissingWrongOrDisabledAccount()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var factory = new PostgreSqlAuthApiFactory(database.ConnectionString);
        var email = $"disabled-{Guid.NewGuid():N}@example.com";
        await CreateAdminAsync(factory, email, disabled: true);
        using var client = CreateClient(factory);

        var missing = await LoginAsync(client, $"missing-{Guid.NewGuid():N}@example.com", Password);
        var wrong = await LoginAsync(client, email, "Wrong-password-123!");
        var disabled = await LoginAsync(client, email, Password);
        var missingProblem = await missing.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        var wrongProblem = await wrong.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        var disabledProblem = await disabled.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, disabled.StatusCode);
        Assert.Equal(
            missingProblem.GetProperty("detail").GetString(),
            wrongProblem.GetProperty("detail").GetString());
        Assert.Equal(
            missingProblem.GetProperty("detail").GetString(),
            disabledProblem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task LogoutRevokesCurrentSessionAndInvalidatesAccessToken()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var factory = new PostgreSqlAuthApiFactory(database.ConnectionString);
        var email = $"logout-{Guid.NewGuid():N}@example.com";
        await CreateAdminAsync(factory, email);
        using var client = CreateClient(factory);
        var login = await LoginAsync(client, email, Password);
        var body = await login.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(body);

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logout.Headers.Add("Origin", FrontendOrigin);
        logout.Headers.Add("X-CSRF-Token", body.Data.CsrfToken);
        logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body.Data.AccessToken);
        var logoutResponse = await client.SendAsync(logout, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        using var sessions = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/sessions");
        sessions.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body.Data.AccessToken);
        var afterLogout = await client.SendAsync(sessions, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password }),
        };
        request.Headers.Add("Origin", FrontendOrigin);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static HttpRequestMessage CreateRefreshRequest(string refreshToken, string csrfToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Origin", FrontendOrigin);
        request.Headers.Add("X-CSRF-Token", csrfToken);
        request.Headers.Add("Cookie", $"__Host-refresh={refreshToken}");
        return request;
    }

    private static string GetCookie(HttpResponseMessage response, string name)
    {
        var header = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith($"{name}=", StringComparison.Ordinal));
        return header[(name.Length + 1)..header.IndexOf(';')];
    }

    private static async Task CreateAdminAsync(
        WebApplicationFactory<Program> factory,
        string email,
        bool disabled = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(AdminBootstrapper.AdminRole))
        {
            var roleResult = await roleManager.CreateAsync(
                new IdentityRole<Guid>(AdminBootstrapper.AdminRole));
            Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(error => error.Description)));
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsDisabled = disabled,
        };
        var userResult = await userManager.CreateAsync(user, Password);
        Assert.True(userResult.Succeeded, string.Join(", ", userResult.Errors.Select(error => error.Description)));
        var roleAssignment = await userManager.AddToRoleAsync(user, AdminBootstrapper.AdminRole);
        Assert.True(roleAssignment.Succeeded, string.Join(", ", roleAssignment.Errors.Select(error => error.Description)));
    }
}

internal sealed class PostgreSqlAuthApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    private readonly TestJwtCertificate _certificate = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:PostgreSql", connectionString);
        builder.UseSetting("Frontend:Origin", AuthenticationFlowTests.FrontendOrigin);
        builder.UseSetting("SupabaseStorage:Url", string.Empty);
        builder.UseSetting("SupabaseStorage:ServiceRoleKey", string.Empty);
        builder.UseSetting("BootstrapAdmin:Enabled", "false");
        PortfolioApiFactory.ApplyAuthenticationSettings(builder, _certificate);
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _certificate.Dispose();
        }
    }
}
