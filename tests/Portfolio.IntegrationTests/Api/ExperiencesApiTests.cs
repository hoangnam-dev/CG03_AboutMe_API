using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Experiences;
using Xunit;

namespace Portfolio.IntegrationTests.Api;

public sealed class ExperiencesApiTests : IClassFixture<DatabaseOptionalApiFactory>
{
    private readonly DatabaseOptionalApiFactory _factory;
    private readonly HttpClient _client;

    public ExperiencesApiTests(DatabaseOptionalApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task SwaggerContainsEveryExperienceRoute()
    {
        var json = await _client.GetStringAsync(
            "/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/portfolio/{slug}/experiences", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/experiences", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/experiences/{id}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/experiences/reorder", out _));
    }

    [Theory]
    [InlineData("/api/v1/admin/experiences")]
    [InlineData("/api/v1/admin/experiences/11111111-1111-1111-1111-111111111111")]
    public async Task AdminExperienceRoutesRejectAnonymousRequests(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AdminExperienceRoutesRejectNonAdminToken()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/experiences");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _factory.CreateToken());

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnsupportedExperienceLocaleReturnsProblemDetailsBeforeDatabaseAccess()
    {
        var response = await _client.GetAsync(
            "/api/v1/portfolio/nam/experiences?locale=fr",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task MissingAdminExperienceReturnsProblemDetails()
    {
        await using var factory = CreateFactory(new ExperienceRepositoryStub());
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/admin/experiences/11111111-1111-1111-1111-111111111111");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", _factory.CreateToken(role: "Admin"));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PartialAdminReorderReturnsConflictProblemDetails()
    {
        await using var factory = CreateFactory(new ExperienceRepositoryStub
        {
            ReorderResult = ExperienceReorderResult.SetMismatch,
        });
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(
            HttpMethod.Patch, "/api/v1/admin/experiences/reorder");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", _factory.CreateToken(role: "Admin"));
        request.Content = JsonContent.Create(new ExperienceReorderRequest(
            [new ExperienceOrderItem(Guid.NewGuid(), 0)]));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private WebApplicationFactory<Program> CreateFactory(IExperienceRepository repository) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IExperienceRepository>();
                services.AddSingleton(repository);
            }));

    private sealed class ExperienceRepositoryStub : IExperienceRepository
    {
        public ExperienceReorderResult ReorderResult { get; init; } = ExperienceReorderResult.Success;

        public Task<IReadOnlyList<PublicExperienceResponse>?> GetPublicAsync(string slug, string locale, CancellationToken token) => Task.FromResult<IReadOnlyList<PublicExperienceResponse>?>([]);
        public Task<ExperienceAdminPage> GetExperiencesAsync(ExperienceAdminQuery query, CancellationToken token) => Task.FromResult(new ExperienceAdminPage([], 0, query.Page, query.PageSize));
        public Task<WorkExperience?> GetAsync(Guid id, bool tracked, CancellationToken token) => Task.FromResult<WorkExperience?>(null);
        public Task<bool> TechnologyIdsExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => Task.FromResult(true);
        public Task AddAsync(WorkExperience experience, CancellationToken token) => Task.CompletedTask;
        public void Remove(WorkExperience experience) { }
        public Task<ExperienceReorderResult> ReorderAsync(IReadOnlyList<ExperienceOrderItem> items, CancellationToken token) => Task.FromResult(ReorderResult);
        public Task SaveChangesAsync(CancellationToken token) => Task.CompletedTask;
    }
}
