using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Portfolio.Application.Common.Authentication;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Common.Storage;
using Portfolio.Application.Resumes;
using Xunit;

namespace Portfolio.IntegrationTests.Api;

public sealed class ResumesApiTests : IClassFixture<DatabaseOptionalApiFactory>
{
    private readonly DatabaseOptionalApiFactory _factory;
    private readonly HttpClient _client;

    public ResumesApiTests(DatabaseOptionalApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task SwaggerContainsEveryResumeRoute()
    {
        var json = await _client.GetStringAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.GetProperty("/api/v1/portfolio/{slug}/cv/current").TryGetProperty("get", out _));
        var collection = paths.GetProperty("/api/v1/admin/cv");
        Assert.True(collection.TryGetProperty("get", out _));
        Assert.True(collection.TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/v1/admin/cv/{id}/current").TryGetProperty("patch", out _));
        Assert.True(paths.GetProperty("/api/v1/admin/cv/{id}/publish").TryGetProperty("patch", out _));
        Assert.True(paths.GetProperty("/api/v1/admin/cv/{id}").TryGetProperty("delete", out _));
    }

    [Theory]
    [InlineData("GET", "/api/v1/admin/cv")]
    [InlineData("POST", "/api/v1/admin/cv")]
    [InlineData("PATCH", "/api/v1/admin/cv/11111111-1111-1111-1111-111111111111/current")]
    [InlineData("PATCH", "/api/v1/admin/cv/11111111-1111-1111-1111-111111111111/publish")]
    [InlineData("DELETE", "/api/v1/admin/cv/11111111-1111-1111-1111-111111111111")]
    public async Task AdminResumeRoutesRejectAnonymousRequests(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnsupportedOrMissingPublicLanguageReturnsBadRequest()
    {
        var unsupported = await _client.GetAsync(
            "/api/v1/portfolio/nam/cv/current?language=fr", TestContext.Current.CancellationToken);
        var missing = await _client.GetAsync(
            "/api/v1/portfolio/nam/cv/current", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task PublicCurrentReturnsSignedUrlWithoutObjectKey()
    {
        await using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IResumeRepository>();
            services.AddSingleton<IResumeRepository>(new RepositoryStub
            {
                Public = new ResumePublicProjection(
                    Guid.NewGuid(), "en", "resumes/private/cv.pdf", "resume.pdf",
                    "application/pdf", 42, 2026, 7, "Current CV"),
            });
            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage, StorageStub>();
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(
            "/api/v1/portfolio/nam/cv/current?language=en",
            TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("https://storage.example/signed", json, StringComparison.Ordinal);
        Assert.Contains("v2026_07", json, StringComparison.Ordinal);
        Assert.DoesNotContain("resumes/private/cv.pdf", json, StringComparison.Ordinal);
        Assert.DoesNotContain("objectKey", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("versionYear")]
    [InlineData("versionSequence")]
    public async Task UploadRejectsClientSuppliedVersionFields(string field)
    {
        await using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAccessSessionValidator>();
            services.AddSingleton<IAccessSessionValidator, ValidSessionValidator>();
            services.RemoveAll<IResumeService>();
            services.AddSingleton<IResumeService, ResumeServiceStub>();
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/cv");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _factory.CreateToken(role: "Admin"));
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "resume.pdf");
        form.Add(new StringContent("en"), "language");
        form.Add(new StringContent("v2026_99"), field);
        request.Content = form;

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class ValidSessionValidator : IAccessSessionValidator
    {
        public Task<bool> IsValidAsync(CurrentAuthSession current, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class ResumeServiceStub : IResumeService
    {
        public Task<ResumePublicResponse> GetPublicCurrentAsync(string slug, string? language, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ResumeAdminPage> GetResumesAsync(ResumeAdminQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ResumeAdminResponse> UploadAsync(ResumeUploadRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("Version-field validation was bypassed.");
        public Task<ResumeAdminResponse> SetCurrentAsync(Guid id, ResumeCurrentRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ResumeAdminResponse> SetPublishedAsync(Guid id, ResumePublishRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RepositoryStub : IResumeRepository
    {
        public ResumePublicProjection? Public { get; init; }

        public Task<ResumePublicProjection?> GetPublicCurrentAsync(string slug, string language, CancellationToken cancellationToken) => Task.FromResult(Public);
        public Task<ResumeEntityPage> GetResumesAsync(ResumeAdminQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ResumeFile?> GetAsync(Guid id, bool tracked, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ResumeFile> CreateVersionAsync(ResumeFile resumeFile, short versionYear, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ResumeActivationResult> SetCurrentAsync(Guid id, DateTimeOffset updatedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Remove(ResumeFile resumeFile) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StorageStub : IFileStorage
    {
        public Task<StorageObject> UploadAsync(StorageUpload upload, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteIfExistsAsync(string bucket, string objectKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Uri GetPublicReadUrl(string bucket, string objectKey) => throw new NotSupportedException();
        public Task<Uri> CreateSignedReadUrlAsync(string bucket, string objectKey, TimeSpan lifetime, CancellationToken cancellationToken) =>
            Task.FromResult(new Uri("https://storage.example/signed"));
    }
}
