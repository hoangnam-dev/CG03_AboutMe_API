using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Portfolio.Infrastructure.Storage;

public sealed class SupabaseStorageHealthCheck(
    HttpClient httpClient,
    IOptions<SupabaseStorageOptions> options) : IHealthCheck
{
    private const string UnavailableDescription = "Supabase Storage is unavailable.";
    private readonly SupabaseStorageOptions _options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "storage/v1/bucket");
        request.Headers.Add("apikey", _options.ServiceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _options.ServiceRoleKey);

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Unhealthy();
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(
                cancellationToken);
            using var bounded = new MemoryStream();
            var buffer = new byte[4096];
            while (true)
            {
                var read = await responseStream.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                if (bounded.Length + read > _options.MaxResponseBytes)
                {
                    return Unhealthy();
                }

                bounded.Write(buffer, 0, read);
            }

            bounded.Position = 0;
            using var document = await JsonDocument.ParseAsync(
                bounded,
                cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Unhealthy();
            }

            var actualBuckets = document.RootElement
                .EnumerateArray()
                .Where(element => element.ValueKind == JsonValueKind.Object)
                .Select(element => element.TryGetProperty("id", out var id) ? id.GetString() : null)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
            var configuredBuckets = new[]
            {
                _options.Buckets.Avatars,
                _options.Buckets.ProjectImages,
                _options.Buckets.CertificateFiles,
                _options.Buckets.CvFiles,
            };

            return configuredBuckets.All(actualBuckets.Contains)
                ? HealthCheckResult.Healthy()
                : Unhealthy();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unhealthy();
        }
        catch (HttpRequestException)
        {
            return Unhealthy();
        }
        catch (JsonException)
        {
            return Unhealthy();
        }
        catch (IOException)
        {
            return Unhealthy();
        }
    }

    private static HealthCheckResult Unhealthy() =>
        HealthCheckResult.Unhealthy(UnavailableDescription);
}
