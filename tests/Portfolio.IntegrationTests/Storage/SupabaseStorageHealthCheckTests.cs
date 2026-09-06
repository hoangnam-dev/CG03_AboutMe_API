using System.Net;
using System.Text;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Portfolio.Infrastructure.Storage;
using Xunit;

namespace Portfolio.IntegrationTests.Storage;

public sealed class SupabaseStorageHealthCheckTests
{
    [Fact]
    public async Task CheckHealthReturnsHealthyWhenAllConfiguredBucketsExist()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            """
            [
              {"id":"avatars"},
              {"id":"project-images"},
              {"id":"certificate-files"},
              {"id":"cv-files"}
            ]
            """));
        var check = CreateHealthCheck(handler);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("/storage/v1/bucket", handler.Request.RequestUri!.AbsolutePath);
        Assert.Equal("service-secret", handler.Request.Headers.GetValues("apikey").Single());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization?.Scheme);
        Assert.Equal("service-secret", handler.Request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task CheckHealthReturnsUnhealthyWhenConfiguredBucketIsMissing()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            """[{"id":"avatars"},{"id":"project-images"}]"""));
        var check = CreateHealthCheck(handler);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Supabase Storage is unavailable.", result.Description);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task CheckHealthReturnsSafeUnhealthyResultForProviderFailure()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(
            HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("service-secret provider detail"),
        });
        var check = CreateHealthCheck(handler);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Supabase Storage is unavailable.", result.Description);
        Assert.DoesNotContain("service-secret", result.Description, StringComparison.Ordinal);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task CheckHealthReturnsSafeUnhealthyResultForTimeout()
    {
        var handler = new RecordingHandler(
            _ => throw new TaskCanceledException("provider timed out with service-secret"));
        var check = CreateHealthCheck(handler);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Supabase Storage is unavailable.", result.Description);
        Assert.Null(result.Exception);
    }

    private static SupabaseStorageHealthCheck CreateHealthCheck(
        HttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://project.supabase.co/"),
            Timeout = TimeSpan.FromSeconds(2),
        };
        var options = Options.Create(new SupabaseStorageOptions
        {
            Url = new Uri("https://project.supabase.co/"),
            ServiceRoleKey = "service-secret",
            MaxResponseBytes = 4096,
        });

        return new SupabaseStorageHealthCheck(client, options);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(responseFactory(request));
        }
    }
}
