using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Storage;
using Portfolio.Infrastructure.Storage;
using Xunit;

namespace Portfolio.IntegrationTests.Storage;

public sealed class SupabaseFileStorageContractTests
{
    [Fact]
    public void PublicUrlEncodesObjectIdentityWithoutCallingProvider()
    {
        var storage = CreateStorage(new RecordingHandler(
            _ => throw new InvalidOperationException("HTTP must not be called")));

        var result = storage.GetPublicReadUrl("avatars", "profiles/a b.png");

        Assert.Equal(
            "https://project.supabase.co/storage/v1/object/public/avatars/profiles/a%20b.png",
            result.AbsoluteUri);
    }

    [Fact]
    public async Task UploadUsesStorageApiAndReturnsDurableObjectIdentity()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"Id\":\"object-id\",\"Key\":\"avatars/profiles/a b.png\"}",
                Encoding.UTF8,
                "application/json"),
        });
        var storage = CreateStorage(handler);
        var upload = new StorageUpload(
            "avatars",
            "profiles/a b.png",
            new MemoryStream([1, 2, 3]),
            "image/png",
            3);

        var result = await storage.UploadAsync(upload, TestContext.Current.CancellationToken);

        Assert.Equal(new StorageObject("avatars", "profiles/a b.png"), result);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal(
            "/storage/v1/object/avatars/profiles/a%20b.png",
            handler.Request.RequestUri!.AbsolutePath);
        Assert.Equal("service-secret", handler.Request.Headers.GetValues("apikey").Single());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization?.Scheme);
        Assert.Equal("service-secret", handler.Request.Headers.Authorization?.Parameter);
        Assert.Equal("image/png", handler.ContentType);
        Assert.Equal([1, 2, 3], handler.Body);
    }

    [Fact]
    public async Task DeleteTreatsNotFoundAsSuccess()
    {
        var handler = new RecordingHandler(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var storage = CreateStorage(handler);

        await storage.DeleteIfExistsAsync(
            "avatars",
            "profiles/missing.png",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Delete, handler.Request!.Method);
    }

    [Theory]
    [InlineData("/object/sign/cv-files/cv.pdf?token=safe")]
    [InlineData("/storage/v1/object/sign/cv-files/cv.pdf?token=safe")]
    [InlineData("storage/v1/object/sign/cv-files/cv.pdf?token=safe")]
    [InlineData("https://project.supabase.co/storage/v1/object/sign/cv-files/cv.pdf?token=safe")]
    public async Task SignedUrlRequestUsesLifetimeAndResolvesRelativeUrl(string providerUrl)
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(new { signedURL = providerUrl }),
                Encoding.UTF8,
                "application/json"),
        });
        var storage = CreateStorage(handler);

        var result = await storage.CreateSignedReadUrlAsync(
            "cv-files",
            "cv.pdf",
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            "https://project.supabase.co/storage/v1/object/sign/cv-files/cv.pdf?token=safe",
            result.AbsoluteUri);
        Assert.Equal("{\"expiresIn\":300}", Encoding.UTF8.GetString(handler.Body!));
    }

    [Fact]
    public async Task UnauthorizedResponseUsesSafeServiceUnavailableError()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("service-secret provider detail"),
        });
        var storage = CreateStorage(handler);

        var exception = await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            storage.DeleteIfExistsAsync(
                "avatars",
                "profile.png",
                TestContext.Current.CancellationToken));

        Assert.DoesNotContain("service-secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TimeoutUsesSafeServiceUnavailableError()
    {
        var handler = new RecordingHandler(
            _ => throw new TaskCanceledException("provider timed out"));
        var storage = CreateStorage(handler);

        var exception = await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            storage.DeleteIfExistsAsync(
                "avatars",
                "profile.png",
                TestContext.Current.CancellationToken));

        Assert.Equal("File storage is temporarily unavailable.", exception.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, typeof(ConflictException))]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, typeof(PayloadTooLargeException))]
    [InlineData(HttpStatusCode.Forbidden, typeof(ServiceUnavailableException))]
    [InlineData(HttpStatusCode.TooManyRequests, typeof(ServiceUnavailableException))]
    [InlineData(HttpStatusCode.InternalServerError, typeof(ServiceUnavailableException))]
    public async Task ProviderFailuresMapToSafeApplicationErrors(
        HttpStatusCode statusCode,
        Type expectedExceptionType)
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("service-secret provider detail"),
        });
        var storage = CreateStorage(handler);

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            storage.DeleteIfExistsAsync(
                "avatars",
                "profile.png",
                TestContext.Current.CancellationToken));

        Assert.IsType(expectedExceptionType, exception);
        Assert.DoesNotContain("service-secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadSuccessWithoutUsableIdentifierIsRejected()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"Id\":null,\"Key\":null}"),
        });
        var storage = CreateStorage(handler);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => storage.UploadAsync(
            new StorageUpload(
                "avatars",
                "profiles/new.png",
                new MemoryStream([1]),
                "image/png",
                1),
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("https://attacker.example/storage/v1/object/sign/cv-files/cv.pdf?token=value")]
    [InlineData("//attacker.example/storage/v1/object/sign/cv-files/cv.pdf?token=value")]
    [InlineData("http://project.supabase.co/storage/v1/object/sign/cv-files/cv.pdf?token=value")]
    [InlineData("https://project.supabase.co:444/storage/v1/object/sign/cv-files/cv.pdf?token=value")]
    [InlineData("file:///storage/v1/object/sign/cv-files/cv.pdf")]
    [InlineData("/storage/v1/object/public/cv-files/cv.pdf")]
    public async Task SignedUrlFromUnexpectedOriginOrPathIsRejected(string providerUrl)
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(new { signedURL = providerUrl }),
                Encoding.UTF8,
                "application/json"),
        });
        var storage = CreateStorage(handler);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            storage.CreateSignedReadUrlAsync(
                "cv-files",
                "cv.pdf",
                TimeSpan.FromMinutes(5),
                TestContext.Current.CancellationToken));
    }

    private static SupabaseFileStorage CreateStorage(HttpMessageHandler handler)
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
        return new SupabaseFileStorage(client, options);
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public byte[]? Body { get; private set; }
        public string? ContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            if (request.Content is not null)
            {
                ContentType = request.Content.Headers.ContentType?.MediaType;
                Body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            }

            return responseFactory(request);
        }
    }
}
