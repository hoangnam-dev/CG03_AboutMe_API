using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Contacts;
using Xunit;

namespace Portfolio.IntegrationTests.Api;

public sealed class ContactsApiTests : IClassFixture<DatabaseOptionalApiFactory>
{
    private readonly DatabaseOptionalApiFactory _factory;

    public ContactsApiTests(DatabaseOptionalApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousPostReturnsReceiptWithoutEchoingPersonalData()
    {
        var service = new ContactServiceStub();
        await using var factory = CreateFactory(service);
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v1/contact",
            Request(),
            TestContext.Current.CancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Message received.", payload.GetProperty("message").GetString());
        Assert.True(payload.GetProperty("data").TryGetProperty("id", out _));
        Assert.True(payload.GetProperty("data").TryGetProperty("receivedAt", out _));
        Assert.DoesNotContain("visitor@example.com", payload.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("Plain text message", payload.GetRawText(), StringComparison.Ordinal);
        Assert.NotNull(service.Metadata);
        Assert.DoesNotContain("127.0.0.1", service.Metadata.IpHash ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidEmailReturnsValidationProblemBeforeServiceCall()
    {
        var service = new ContactServiceStub();
        await using var factory = CreateFactory(service);
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v1/contact",
            Request() with { SenderEmail = "invalid" },
            TestContext.Current.CancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(payload.TryGetProperty("errors", out _));
        Assert.Equal(0, service.CreateCalls);
    }

    [Fact]
    public async Task FilledHoneypotReturnsSameExternalShapeAsNormalSubmission()
    {
        var service = new ContactServiceStub();
        await using var factory = CreateFactory(service);
        using var client = CreateClient(factory);

        var normal = await client.PostAsJsonAsync(
            "/api/v1/contact", Request(), TestContext.Current.CancellationToken);
        var spam = await client.PostAsJsonAsync(
            "/api/v1/contact",
            Request() with { Website = "https://spam.invalid" },
            TestContext.Current.CancellationToken);
        var normalPayload = await normal.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        var spamPayload = await spam.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, normal.StatusCode);
        Assert.Equal(normal.StatusCode, spam.StatusCode);
        Assert.Equal(normalPayload.GetProperty("message").GetString(), spamPayload.GetProperty("message").GetString());
        Assert.Equal(
            normalPayload.GetProperty("data").EnumerateObject().Select(property => property.Name).Order(),
            spamPayload.GetProperty("data").EnumerateObject().Select(property => property.Name).Order());
    }

    [Fact]
    public async Task NotificationFailureStillReturnsCreatedAfterPersistence()
    {
        var repository = new ContactRepositoryStub();
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IContactRepository>();
                services.RemoveAll<IContactNotifier>();
                services.AddSingleton<IContactRepository>(repository);
                services.AddSingleton<IContactNotifier, ThrowingNotifier>();
            }));
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v1/contact", Request(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(repository.Saved);
    }

    [Fact]
    public async Task SpoofedForwardedHeadersDoNotBypassConnectionRateLimit()
    {
        await using var factory = CreateFactory(new ContactServiceStub(), permitLimit: 2);
        using var client = CreateClient(factory);

        var responses = new List<HttpResponseMessage>();
        for (var index = 0; index < 3; index++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/contact")
            {
                Content = JsonContent.Create(Request()),
            };
            request.Headers.Add("X-Forwarded-For", $"198.51.100.{index + 1}");
            responses.Add(await client.SendAsync(request, TestContext.Current.CancellationToken));
        }

        Assert.Equal(HttpStatusCode.Created, responses[0].StatusCode);
        Assert.Equal(HttpStatusCode.Created, responses[1].StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, responses[2].StatusCode);
        Assert.Equal("application/problem+json", responses[2].Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task OversizedContactBodyReturnsPayloadTooLarge()
    {
        await using var factory = CreateFactory(new ContactServiceStub());
        using var client = CreateClient(factory);
        using var content = JsonContent.Create(new
        {
            senderName = "Fictional Visitor",
            senderEmail = "visitor@example.com",
            subject = "Project inquiry",
            message = new string('a', 70_000),
            website = string.Empty,
        });

        var response = await client.PostAsync(
            "/api/v1/contact", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task PublicContactRouteDoesNotExposeInboxReads()
    {
        await using var factory = CreateFactory(new ContactServiceStub());
        using var client = CreateClient(factory);

        var response = await client.GetAsync(
            "/api/v1/contact", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/admin/contacts")]
    [InlineData("/api/v1/admin/contacts/11111111-1111-1111-1111-111111111111")]
    public async Task AdminInboxRejectsAnonymousRequests(string path)
    {
        await using var factory = CreateFactory(new ContactServiceStub());
        using var client = CreateClient(factory);

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AdminInboxRejectsNonAdminToken()
    {
        await using var factory = CreateFactory(new ContactServiceStub());
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/contacts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _factory.CreateToken());

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminCanListReadUpdateAndDeleteContacts()
    {
        var service = new ContactServiceStub();
        await using var factory = CreateFactory(service);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", _factory.CreateToken(role: "Admin"));
        var id = service.Response.Id;

        var list = await client.GetAsync(
            "/api/v1/admin/contacts?page=2&pageSize=10&status=new&search=visitor",
            TestContext.Current.CancellationToken);
        var detail = await client.GetAsync(
            $"/api/v1/admin/contacts/{id}", TestContext.Current.CancellationToken);
        var status = await client.PatchAsJsonAsync(
            $"/api/v1/admin/contacts/{id}/status",
            new ContactStatusRequest(ContactStatus.Read),
            TestContext.Current.CancellationToken);
        var delete = await client.DeleteAsync(
            $"/api/v1/admin/contacts/{id}", TestContext.Current.CancellationToken);
        var listPayload = await list.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        var statusPayload = await status.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(2, listPayload.GetProperty("meta").GetProperty("page").GetInt32());
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Equal("Read", statusPayload.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(ContactStatus.New, service.LastQuery!.Status);
        Assert.Equal("visitor", service.LastQuery.Search);
        Assert.Equal(ContactStatus.Read, service.LastStatus);
        Assert.Equal(id, service.DeletedId);
    }

    [Fact]
    public async Task StatusUpdateRequiresStatusField()
    {
        var service = new ContactServiceStub();
        await using var factory = CreateFactory(service);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", _factory.CreateToken(role: "Admin"));

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/contacts/{service.Response.Id}/status",
            new { },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(service.LastStatus);
    }

    [Fact]
    public async Task SwaggerContainsEveryContactRoute()
    {
        await using var factory = CreateFactory(new ContactServiceStub());
        using var client = CreateClient(factory);
        var json = await client.GetStringAsync(
            "/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/contact", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/contacts", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/contacts/{id}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/admin/contacts/{id}/status", out _));
    }

    private WebApplicationFactory<Program> CreateFactory(
        IContactService service,
        int permitLimit = 100) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                "RateLimit:Contact:PermitLimit",
                permitLimit.ToString(CultureInfo.InvariantCulture));
            builder.UseSetting("RateLimit:Contact:WindowSeconds", "60");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IContactService>();
                services.AddSingleton(service);
            });
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static ContactCreateRequest Request() => new(
        "Fictional Visitor",
        "visitor@example.com",
        "Project inquiry",
        "Plain text message.",
        string.Empty);

    private sealed class ContactServiceStub : IContactService
    {
        public ContactAdminResponse Response { get; } = new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Fictional Visitor",
            "visitor@example.com",
            "Project inquiry",
            "Plain text message.",
            ContactStatus.New,
            false,
            "privacy-preserving-hash",
            "test-agent",
            new DateTimeOffset(2026, 9, 8, 8, 30, 0, TimeSpan.Zero),
            null);

        public int CreateCalls { get; private set; }
        public ContactSubmissionMetadata? Metadata { get; private set; }
        public ContactAdminQuery? LastQuery { get; private set; }
        public ContactStatus? LastStatus { get; private set; }
        public Guid? DeletedId { get; private set; }

        public Task<ContactReceiptResponse> CreateContactAsync(ContactCreateRequest request, ContactSubmissionMetadata metadata, CancellationToken cancellationToken)
        {
            CreateCalls++;
            Metadata = metadata;
            return Task.FromResult(new ContactReceiptResponse(Response.Id, Response.CreatedAt));
        }

        public Task<ContactAdminPage> GetContactsAsync(ContactAdminQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult(new ContactAdminPage([Response], 11, query.Page, query.PageSize));
        }

        public Task<ContactAdminResponse> GetContactAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Response);

        public Task<ContactAdminResponse> UpdateStatusAsync(Guid id, ContactStatusRequest request, CancellationToken cancellationToken)
        {
            LastStatus = request.Status;
            return Task.FromResult(Response with { Status = request.Status!.Value });
        }

        public Task DeleteContactAsync(Guid id, CancellationToken cancellationToken)
        {
            DeletedId = id;
            return Task.CompletedTask;
        }
    }

    private sealed class ContactRepositoryStub : IContactRepository
    {
        public bool Saved { get; private set; }

        public Task<ContactMessagePage> GetPageAsync(ContactAdminQuery query, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<ContactMessage?> GetAsync(Guid id, bool tracked, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task AddAsync(ContactMessage message, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void Remove(ContactMessage message) => throw new NotImplementedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingNotifier : IContactNotifier
    {
        public Task NotifyAsync(ContactMessage message, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Provider unavailable.");
    }
}
