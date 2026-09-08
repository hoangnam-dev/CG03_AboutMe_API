using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.IntegrationTests.Api;
using Xunit;

namespace Portfolio.IntegrationTests.Security;

public sealed partial class AdminAuthorizationTests : IClassFixture<DatabaseOptionalApiFactory>
{
    private readonly DatabaseOptionalApiFactory _factory;

    public AdminAuthorizationTests(DatabaseOptionalApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EveryAdminOperationEnforcesAdministratorPolicy()
    {
        using var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var operations = GetAdminOperations(_factory.Services).ToArray();
        var failures = new List<string>();

        Assert.NotEmpty(operations);

        foreach (var operation in operations)
        {
            var anonymous = await SendAsync(client, operation, role: null);
            if (anonymous != HttpStatusCode.Unauthorized)
            {
                failures.Add($"{operation} returned {(int)anonymous} for an anonymous caller.");
            }

            var nonAdmin = await SendAsync(client, operation, role: "User");
            if (nonAdmin != HttpStatusCode.Forbidden)
            {
                failures.Add($"{operation} returned {(int)nonAdmin} for a non-admin caller.");
            }

            var admin = await SendAsync(client, operation, role: "Admin");
            if (admin is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                failures.Add($"{operation} did not pass authorization for an Administrator caller.");
            }
        }

        Assert.Empty(failures);
    }

    private async Task<HttpStatusCode> SendAsync(
        HttpClient client,
        AdminOperation operation,
        string? role)
    {
        using var request = new HttpRequestMessage(operation.Method, operation.Path);
        if (RequiresMultipart(operation))
        {
            request.Content = new MultipartFormDataContent();
        }
        else if (operation.Method == HttpMethod.Post ||
            operation.Method == HttpMethod.Put ||
            operation.Method.Method == "PATCH")
        {
            request.Content = JsonContent.Create(new { });
        }

        if (role is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                _factory.CreateToken(role));
        }

        using var response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private static bool RequiresMultipart(AdminOperation operation) =>
        operation.Method == HttpMethod.Post &&
        (operation.Path == "/api/v1/admin/cv" ||
         operation.Path.EndsWith("/avatar", StringComparison.Ordinal) ||
         operation.Path.EndsWith("/hero-image", StringComparison.Ordinal) ||
         operation.Path.EndsWith("/images", StringComparison.Ordinal) ||
         operation.Path.EndsWith("/file", StringComparison.Ordinal) ||
         operation.Path.EndsWith("/icon", StringComparison.Ordinal));

    private static IEnumerable<AdminOperation> GetAdminOperations(IServiceProvider services)
    {
        var endpoints = services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is not null);

        foreach (var endpoint in endpoints)
        {
            var route = endpoint.RoutePattern.RawText;
            if (route is null || !route.StartsWith("api/v1/admin", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = "/" + RouteParameterPattern().Replace(route, MatchRouteParameter);
            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
            foreach (var method in methods)
            {
                yield return new AdminOperation(new HttpMethod(method), path);
            }
        }
    }

    private static string MatchRouteParameter(Match match)
    {
        var parameter = match.Groups[1].Value;
        return parameter.Contains(":guid", StringComparison.Ordinal)
            ? "11111111-1111-1111-1111-111111111111"
            : "test";
    }

    [GeneratedRegex("\\{([^}]+)\\}", RegexOptions.CultureInvariant)]
    private static partial Regex RouteParameterPattern();

    private sealed record AdminOperation(HttpMethod Method, string Path)
    {
        public override string ToString() => $"{Method.Method} {Path}";
    }
}
