using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Storage;

namespace Portfolio.Infrastructure.Storage;

public sealed class SupabaseFileStorage(
    HttpClient httpClient,
    IOptions<SupabaseStorageOptions> options) : IFileStorage
{
    private readonly SupabaseStorageOptions _options = options.Value;

    public Uri GetPublicReadUrl(string bucket, string objectKey)
    {
        ValidateIdentity(bucket, objectKey);
        return new Uri(
            _options.Url!,
            $"storage/v1/object/public/{BuildObjectPath(bucket, objectKey)}");
    }

    public async Task<StorageObject> UploadAsync(
        StorageUpload upload,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(upload.Bucket, upload.ObjectKey);
        using var request = CreateRequest(
            HttpMethod.Post,
            $"storage/v1/object/{BuildObjectPath(upload.Bucket, upload.ObjectKey)}");
        request.Headers.Add("x-upsert", "false");
        request.Content = new StreamContent(upload.Content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(upload.ContentType);
        request.Content.Headers.ContentLength = upload.Length;

        using var response = await SendAsync(request, cancellationToken);
        var payload = await ReadJsonAsync<UploadResponse>(response, cancellationToken);
        if (string.IsNullOrWhiteSpace(payload.Id) ||
            !string.Equals(
                payload.Key,
                $"{upload.Bucket}/{upload.ObjectKey}",
                StringComparison.Ordinal))
        {
            throw Unavailable();
        }

        return new StorageObject(upload.Bucket, upload.ObjectKey);
    }

    public async Task DeleteIfExistsAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(bucket, objectKey);
        using var request = CreateRequest(
            HttpMethod.Delete,
            $"storage/v1/object/{BuildObjectPath(bucket, objectKey)}");
        using var response = await SendRawAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        EnsureSuccess(response);
    }

    public async Task<Uri> CreateSignedReadUrlAsync(
        string bucket,
        string objectKey,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(bucket, objectKey);
        var seconds = checked((int)lifetime.TotalSeconds);
        if (seconds is < 1 or > 86_400)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                "Signed URL lifetime must be between 1 second and 24 hours.");
        }

        using var request = CreateRequest(
            HttpMethod.Post,
            $"storage/v1/object/sign/{BuildObjectPath(bucket, objectKey)}");
        request.Content = JsonContent.Create(new { expiresIn = seconds });
        using var response = await SendAsync(request, cancellationToken);
        var payload = await ReadJsonAsync<SignedUrlResponse>(response, cancellationToken);
        if (string.IsNullOrWhiteSpace(payload.SignedUrl))
        {
            throw Unavailable();
        }

        var relative = payload.SignedUrl.StartsWith("/object/", StringComparison.Ordinal)
            ? $"/storage/v1{payload.SignedUrl}"
            : payload.SignedUrl;
        var signedUrl = Uri.TryCreate(relative, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(_options.Url!, relative);
        if (!string.Equals(signedUrl.Scheme, _options.Url!.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(signedUrl.Host, _options.Url.Host, StringComparison.OrdinalIgnoreCase) ||
            signedUrl.Port != _options.Url.Port ||
            !signedUrl.AbsolutePath.StartsWith(
                "/storage/v1/object/sign/",
                StringComparison.Ordinal))
        {
            throw Unavailable();
        }

        return signedUrl;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("apikey", _options.ServiceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _options.ServiceRoleKey);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await SendRawAsync(request, cancellationToken);
        try
        {
            EnsureSuccess(response);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private async Task<HttpResponseMessage> SendRawAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Unavailable();
        }
        catch (HttpRequestException)
        {
            throw Unavailable();
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new ConflictException("A storage object already exists at the generated key.");
        }

        if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
        {
            throw new PayloadTooLargeException("The storage provider rejected the file size.");
        }

        throw Unavailable();
    }

    private async Task<T> ReadJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
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
                    throw Unavailable();
                }

                bounded.Write(buffer, 0, read);
            }

            bounded.Position = 0;
            return await JsonSerializer.DeserializeAsync<T>(
                       bounded,
                       cancellationToken: cancellationToken)
                   ?? throw Unavailable();
        }
        catch (JsonException)
        {
            throw Unavailable();
        }
    }

    private static string BuildObjectPath(string bucket, string objectKey) =>
        string.Join('/', new[] { bucket }.Concat(objectKey.Split('/'))
            .Select(Uri.EscapeDataString));

    private static void ValidateIdentity(string bucket, string objectKey)
    {
        if (string.IsNullOrWhiteSpace(bucket) ||
            string.IsNullOrWhiteSpace(objectKey) ||
            objectKey.StartsWith('/') ||
            objectKey.Contains('\\') ||
            objectKey.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("Bucket and object key must be server-generated safe values.");
        }
    }

    private static ServiceUnavailableException Unavailable() =>
        new("File storage is temporarily unavailable.");

    private sealed record UploadResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("Id")] string? Id,
        [property: System.Text.Json.Serialization.JsonPropertyName("Key")] string? Key);

    private sealed record SignedUrlResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("signedURL")] string? SignedUrl);
}
