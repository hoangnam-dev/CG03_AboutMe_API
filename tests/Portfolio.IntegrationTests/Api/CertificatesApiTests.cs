using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Portfolio.Application.Certificates;
using Portfolio.Application.Common.Models;
using Xunit;

namespace Portfolio.IntegrationTests.Api;

public sealed class CertificatesApiTests : IClassFixture<DatabaseOptionalApiFactory>
{
    private readonly DatabaseOptionalApiFactory _factory;
    private readonly HttpClient _client;

    public CertificatesApiTests(DatabaseOptionalApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task SwaggerContainsEveryCertificateRoute()
    {
        var json = await _client.GetStringAsync(
            "/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/portfolio/{slug}/certificates", out var publicPath));
        Assert.True(publicPath.TryGetProperty("get", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/certificates", out var collection));
        Assert.True(collection.TryGetProperty("get", out _));
        Assert.True(collection.TryGetProperty("post", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/certificates/{id}", out var item));
        Assert.True(item.TryGetProperty("get", out _));
        Assert.True(item.TryGetProperty("put", out _));
        Assert.True(item.TryGetProperty("delete", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/certificates/{id}/file", out var file));
        Assert.True(file.TryGetProperty("post", out _));
    }

    [Theory]
    [InlineData("GET", "/api/v1/admin/certificates")]
    [InlineData("GET", "/api/v1/admin/certificates/11111111-1111-1111-1111-111111111111")]
    [InlineData("POST", "/api/v1/admin/certificates/11111111-1111-1111-1111-111111111111/file")]
    public async Task AdminCertificateRoutesRejectAnonymousRequests(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AdminCertificateRoutesRejectNonAdminToken()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/certificates");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _factory.CreateToken());

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnsupportedCertificateLocaleReturnsProblemDetailsBeforeDatabaseAccess()
    {
        var response = await _client.GetAsync(
            "/api/v1/portfolio/nam/certificates?locale=fr",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task HiddenCredentialIdIsAbsentFromPublicJson()
    {
        var projection = new CertificatePublicProjection(
            Guid.NewGuid(), "Issuer", new DateOnly(2026, 1, 1), null,
            "SECRET", false, "https://example.com/verify", 0, "Cloud", null, null, []);
        await using var factory = CreateFactory(new RepositoryStub { PublicItems = [projection] });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(
            "/api/v1/portfolio/nam/certificates?locale=en",
            TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("credentialId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("objectKey", json, StringComparison.OrdinalIgnoreCase);
    }

    private WebApplicationFactory<Program> CreateFactory(ICertificateRepository repository) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICertificateRepository>();
            services.AddSingleton(repository);
        }));

    private sealed class RepositoryStub : ICertificateRepository
    {
        public IReadOnlyList<CertificatePublicProjection>? PublicItems { get; init; } = [];

        public Task<IReadOnlyList<CertificatePublicProjection>?> GetPublicAsync(string slug, string locale, CancellationToken token) => Task.FromResult(PublicItems);
        public Task<CertificateEntityPage> GetCertificatesAsync(CertificateAdminQuery query, CancellationToken token) => Task.FromResult(new CertificateEntityPage([], 0, query.Page, query.PageSize));
        public Task<Certificate?> GetAsync(Guid id, bool tracked, CancellationToken token) => Task.FromResult<Certificate?>(null);
        public Task<bool> TechnologyIdsExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => Task.FromResult(true);
        public Task AddAsync(Certificate certificate, CancellationToken token) => Task.CompletedTask;
        public void Remove(Certificate certificate) { }
        public Task SaveChangesAsync(CancellationToken token) => Task.CompletedTask;
    }
}
