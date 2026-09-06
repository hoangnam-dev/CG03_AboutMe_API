using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Portfolio.Api.Health;

public static class HealthCheckResponseWriter
{
    public static Task WriteAsync(HttpContext httpContext, HealthReport report)
    {
        var checks = report.Entries
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(
                entry => entry.Key,
                entry => new ComponentHealthResponse(
                    Normalize(entry.Value.Status),
                    entry.Value.Duration.TotalMilliseconds),
                StringComparer.Ordinal);
        var response = new SupabaseHealthResponse(
            Normalize(report.Status),
            report.TotalDuration.TotalMilliseconds,
            checks);

        return httpContext.Response.WriteAsJsonAsync(
            response,
            httpContext.RequestAborted);
    }

    private static string Normalize(HealthStatus status) =>
        status.ToString().ToLowerInvariant();

    private sealed record SupabaseHealthResponse(
        string Status,
        double TotalDurationMs,
        IReadOnlyDictionary<string, ComponentHealthResponse> Checks);

    private sealed record ComponentHealthResponse(
        string Status,
        double DurationMs);
}
