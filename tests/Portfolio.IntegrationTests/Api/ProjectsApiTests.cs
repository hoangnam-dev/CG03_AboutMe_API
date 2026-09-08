using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Projects;
using Xunit;

namespace Portfolio.IntegrationTests.Api;

public sealed class ProjectsApiTests : IClassFixture<DatabaseOptionalApiFactory>
{
    private readonly DatabaseOptionalApiFactory _factory;
    private readonly HttpClient _client;

    public ProjectsApiTests(DatabaseOptionalApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task SwaggerContainsEveryProjectRoute()
    {
        var json = await _client.GetStringAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/portfolio/{slug}/projects", out _));
        Assert.True(paths.TryGetProperty("/api/v1/portfolio/{slug}/projects/{projectSlug}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/projects", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/projects/{id}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/projects/{id}/publish", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/projects/reorder", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/projects/{id}/images", out _));
    }

    [Theory]
    [InlineData("/api/v1/admin/projects")]
    [InlineData("/api/v1/admin/projects/11111111-1111-1111-1111-111111111111")]
    public async Task AdminProjectRoutesRejectAnonymousRequests(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AdminProjectRoutesRejectNonAdminToken()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/projects");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _factory.CreateToken());

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnsupportedProjectLocaleReturnsProblemDetailsBeforeDatabaseAccess()
    {
        await using var factory = CreateFactory(new ProjectRepositoryStub());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(
            "/api/v1/portfolio/nam/projects?locale=fr", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task LimitedProjectDetailOmitsSensitiveJsonProperties()
    {
        await using var factory = CreateFactory(new ProjectRepositoryStub
        {
            PublicDetail = new ProjectPublicProjection(
                Guid.NewGuid(), "client", "Client", "Summary",
                ProjectKind.Professional, ProjectDisclosureLevel.Limited,
                null, "https://github.com/example/private", "https://example.com/private",
                null, null, false, "Secret", "Engineer", "Problem", "Solution", "Result",
                [], [], [new(Guid.NewGuid(), "projects/client/image.png", "Screenshot", 0)]),
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(
            "/api/v1/portfolio/nam/projects/client?locale=en", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var data = document.RootElement.GetProperty("data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("professional", data.GetProperty("kind").GetString());
        Assert.Equal("limited", data.GetProperty("disclosureLevel").GetString());
        Assert.False(data.TryGetProperty("repositoryUrl", out _));
        Assert.False(data.TryGetProperty("demoUrl", out _));
        Assert.False(data.TryGetProperty("clientContext", out _));
        Assert.False(data.TryGetProperty("problem", out _));
        Assert.False(data.TryGetProperty("solution", out _));
        Assert.False(data.TryGetProperty("result", out _));
        Assert.False(data.TryGetProperty("images", out _));
    }

    [Fact]
    public async Task MalformedGalleryMetadataReturnsProblemDetails()
    {
        await using var factory = CreateFactory(new ProjectRepositoryStub());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/admin/projects/11111111-1111-1111-1111-111111111111/images");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _factory.CreateToken(role: "Admin"));
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "files", "image.png");
        form.Add(new StringContent("not-json"), "metadata");
        request.Content = form;

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private WebApplicationFactory<Program> CreateFactory(IProjectRepository repository) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IProjectRepository>();
            services.AddSingleton(repository);
        }));

    private sealed class ProjectRepositoryStub : IProjectRepository
    {
        public ProjectPublicProjection? PublicDetail { get; init; }
        public Task<IReadOnlyList<ProjectPublicProjection>?> GetPublicListAsync(string slug, string locale, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<ProjectPublicProjection>?>([]);
        public Task<ProjectPublicProjection?> GetPublicDetailAsync(string slug, string projectSlug, string locale, CancellationToken token) => Task.FromResult(PublicDetail);
        public Task<ProjectEntityPage> GetProjectsAsync(ProjectAdminQuery query, CancellationToken token) => Task.FromResult(new ProjectEntityPage([], 0, query.Page, query.PageSize));
        public Task<Project?> GetAsync(Guid id, bool tracked, CancellationToken token) => Task.FromResult<Project?>(null);
        public Task<bool> SlugExistsAsync(string slug, Guid? excludedId, CancellationToken token) => Task.FromResult(false);
        public Task<bool> TechnologyIdsExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => Task.FromResult(true);
        public Task AddAsync(Project project, CancellationToken token) => Task.CompletedTask;
        public void Remove(Project project) { }
        public Task<ProjectReorderResult> ReorderAsync(IReadOnlyList<ProjectOrderItem> items, CancellationToken token) => Task.FromResult(ProjectReorderResult.Success);
        public Task SaveChangesAsync(CancellationToken token) => Task.CompletedTask;
    }
}
