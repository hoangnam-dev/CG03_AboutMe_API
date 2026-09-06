using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Portfolio.Api.Health;
using Xunit;

namespace Portfolio.IntegrationTests.Api;

public sealed class HealthCheckResponseWriterTests
{
    [Fact]
    public async Task WriteAsyncReturnsSafeComponentStatusWithoutDiagnosticDetails()
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["postgresql"] = Entry(HealthStatus.Healthy, null),
            ["supabase-storage"] = Entry(
                HealthStatus.Unhealthy,
                new InvalidOperationException(
                    "Password=secret;ServiceRoleKey=secret;provider response")),
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(12));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await HealthCheckResponseWriter.WriteAsync(context, report);

        context.Response.Body.Position = 0;
        using var payload = await JsonDocument.ParseAsync(
            context.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);
        var checks = payload.RootElement.GetProperty("checks");

        Assert.Equal("unhealthy", payload.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            "healthy",
            checks.GetProperty("postgresql").GetProperty("status").GetString());
        Assert.Equal(
            "unhealthy",
            checks.GetProperty("supabase-storage").GetProperty("status").GetString());
        Assert.DoesNotContain(
            "Password",
            payload.RootElement.ToString(),
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "ServiceRoleKey",
            payload.RootElement.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static HealthReportEntry Entry(
        HealthStatus status,
        Exception? exception) => new(
        status,
        "provider diagnostic detail",
        TimeSpan.FromMilliseconds(5),
        exception,
        new Dictionary<string, object>
        {
            ["connectionString"] = "Password=secret",
        });
}
