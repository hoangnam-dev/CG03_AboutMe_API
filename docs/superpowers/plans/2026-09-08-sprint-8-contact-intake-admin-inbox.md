# Sprint 8 Contact Intake and Admin Inbox Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver validated, abuse-resistant public Contact Message intake and an administrator-only paginated inbox while preserving notification-failure durability and dashboard unread counts.

**Architecture:** Add a `Contacts` Application feature whose service validates and maps requests, persists through a feature-specific repository, and invokes an abstract notifier only after commit. Infrastructure supplies EF Core persistence plus a safe logging/no-op notifier, while API controllers own HTTP metadata, trusted connection-IP hashing, rate limiting, authorization, response wrappers, and OpenAPI declarations.

**Tech Stack:** .NET 10, ASP.NET Core 10 controllers and rate limiting, C#, EF Core 10, Npgsql/PostgreSQL, Serilog-compatible `ILogger`, xUnit v3, `WebApplicationFactory`.

**Spec:** `docs/superpowers/plans/2026-09-05-personal-portfolio-backend-roadmap.md` Sprint 8, `docs/api/API_CONTRACT.md` section 9, `docs/security/THREAT_MODEL.md` Contact controls, and `docs/database.md` `contact_messages`.

## Global Constraints

- Keep `Controller -> IContactService -> ContactService -> IContactRepository -> ContactRepository -> PortfolioDbContext`.
- Public callers can create Contact Messages but no public inbox read route may exist.
- Validate sender name 1-150, valid sender email at most 320, subject 1-200, message 1-5000, and honeypot `website` at most 200.
- A filled honeypot persists a message with `IsSpam = true` and returns the same 201 response contract as a normal submission.
- Persist before notification; notification exceptions are logged without exposing personal data and never change the committed 201 result.
- JSON statuses are `New`, `Read`, and `Archived` semantically; PostgreSQL stores `new`, `read`, and `archived` under the existing constraint.
- Status transitions set `ReadAt` when first entering Read, clear it when returning to New, and preserve it when entering Archived.
- List order is `CreatedAt DESC, Id`; search covers sender name, sender email, and subject; page is at least 1 and page size is 1-100.
- Rate-limit partitions use only normalized `HttpContext.Connection.RemoteIpAddress`, with one bounded `unknown` fallback; arbitrary client headers are not limiter keys.
- Store only a SHA-256 hash of the normalized connection IP, never the raw IP; bound user agent to 500 characters.
- No schema migration is created because the approved table, constraint, and index already exist.
- Follow `docs/BUG_LESSONS.md` test-factory configuration-isolation prevention rule.

---

### Task 1: Contact application behavior

**Files:**
- Create: `src/Portfolio.Application/Contacts/ContactContracts.cs`
- Create: `src/Portfolio.Application/Contacts/IContactRepository.cs`
- Create: `src/Portfolio.Application/Contacts/IContactNotifier.cs`
- Create: `src/Portfolio.Application/Contacts/IContactService.cs`
- Create: `src/Portfolio.Application/Contacts/ContactService.cs`
- Modify: `src/Portfolio.Application/DependencyInjection.cs`
- Test: `tests/Portfolio.UnitTests/Contacts/ContactServiceTests.cs`

**Interfaces:**
- Consumes: existing `ContactMessage`, `ContactStatus`, `ValidationException`, `NotFoundException`, `TimeProvider`, and `ILogger<ContactService>`.
- Produces: `CreateContactAsync(ContactCreateRequest, ContactSubmissionMetadata, CancellationToken)`, `GetContactsAsync(ContactAdminQuery, CancellationToken)`, `GetContactAsync(Guid, CancellationToken)`, `UpdateStatusAsync(Guid, ContactStatusRequest, CancellationToken)`, and `DeleteContactAsync(Guid, CancellationToken)`.

- [x] **Step 1: Write failing service tests**

```csharp
[Fact]
public async Task CreatePersistsBeforeNotificationAndSwallowsNotifierFailure()
{
    var repository = new ContactRepositoryStub();
    var notifier = new ThrowingContactNotifier(repository);
    var service = CreateService(repository, notifier);
    var result = await service.CreateContactAsync(ValidRequest(), new("ip-hash", "agent"), Token);
    Assert.True(repository.SavedBeforeNotification);
    Assert.NotEqual(Guid.Empty, result.Id);
}

[Fact]
public async Task MarkReadSetsReadAtAndReturningToNewClearsIt()
{
    var message = ExistingMessage(ContactStatus.New);
    var service = CreateService(new ContactRepositoryStub(message), new RecordingNotifier());
    await service.UpdateStatusAsync(message.Id, new(ContactStatus.Read), Token);
    Assert.NotNull(message.ReadAt);
    await service.UpdateStatusAsync(message.Id, new(ContactStatus.New), Token);
    Assert.Null(message.ReadAt);
}
```

- [x] **Step 2: Run the Contacts unit suite and confirm it fails because the feature types do not exist**

Run: `dotnet test tests/Portfolio.UnitTests/Portfolio.UnitTests.csproj --configuration Release --filter FullyQualifiedName~ContactServiceTests`

Expected: compilation failure naming the missing `Portfolio.Application.Contacts` contracts.

- [x] **Step 3: Implement the contracts and minimal service**

```csharp
public sealed record ContactCreateRequest(string SenderName, string SenderEmail, string Subject, string Message, string? Website);
public sealed record ContactSubmissionMetadata(string? IpHash, string? UserAgent);
public sealed record ContactReceiptResponse(Guid Id, DateTimeOffset ReceivedAt);
public sealed record ContactStatusRequest(ContactStatus Status);
public sealed record ContactAdminQuery(int Page, int PageSize, ContactStatus? Status, string? Search);
public sealed record ContactAdminPage(IReadOnlyList<ContactAdminResponse> Items, long Total, int Page, int PageSize)
{
    public int TotalPages => Total == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}
```

Implement validation, trimming, timestamp behavior, persistence-before-notification, safe warning logs containing only Contact ID, list query validation, not-found handling, and delete/status audit logs. Register `IContactService` in Application DI.

- [x] **Step 4: Run the Contacts unit suite until green**

Run: `dotnet test tests/Portfolio.UnitTests/Portfolio.UnitTests.csproj --configuration Release --filter FullyQualifiedName~ContactServiceTests`

Expected: PASS.

### Task 2: PostgreSQL repository and availability fallback

**Files:**
- Create: `src/Portfolio.Infrastructure/Persistence/Repositories/ContactRepository.cs`
- Create: `src/Portfolio.Infrastructure/Availability/DatabaseUnavailableContactRepository.cs`
- Create: `src/Portfolio.Infrastructure/Notifications/LoggingContactNotifier.cs`
- Modify: `src/Portfolio.Infrastructure/DependencyInjection.cs`
- Test: `tests/Portfolio.IntegrationTests/Persistence/ContactRepositoryTests.cs`
- Test: `tests/Portfolio.IntegrationTests/Persistence/PersistenceConstraintTests.cs`

**Interfaces:**
- Consumes: the Task 1 `IContactRepository`, `IContactNotifier`, query/page contracts, and existing `PortfolioDbContext.ContactMessages`.
- Produces: SQL-side filtering/search/order/pagination, tracked detail reads for mutations, persistence operations, and the approved no-op notifier implementation.

- [x] **Step 1: Write failing PostgreSQL tests**

```csharp
[Fact]
public async Task AdminListFiltersSearchesAndOrdersNewestFirst()
{
    // Seed fictional Contact Messages with distinct statuses/timestamps.
    var page = await repository.GetPageAsync(new(1, 20, ContactStatus.New, "visitor"), Token);
    Assert.Equal(expectedIdsNewestFirst, page.Items.Select(x => x.Id));
}

[Fact]
public async Task RejectsUnsupportedContactStatus()
{
    // Insert status outside new/read/archived through SQL and assert ck_contact_messages_status.
}
```

- [x] **Step 2: Run the narrow persistence tests and confirm the missing repository/constraint test fails**

Run: `dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~ContactRepositoryTests|FullyQualifiedName~RejectsUnsupportedContactStatus`

Expected: compilation failure for the missing repository, followed by a PostgreSQL check-constraint assertion once compiled.

- [x] **Step 3: Implement repository, no-op notifier, DI, and unavailable fallback**

Use `AsNoTracking()` for list/detail reads, `EF.Functions.ILike` for bounded search, `OrderByDescending(CreatedAt).ThenBy(Id)`, SQL `Skip/Take`, tracked lookup for update/delete, and `SaveChangesAsync`. The notifier logs only the Contact ID. Register real/fallback repositories along the existing database-configured branches and register the notifier independently.

- [x] **Step 4: Run the Contact persistence suite until green**

Run: `dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~ContactRepositoryTests|FullyQualifiedName~RejectsUnsupportedContactStatus`

Expected: PASS when Docker/PostgreSQL is available.

### Task 3: Public intake, admin inbox, privacy, and rate limiting

**Files:**
- Create: `src/Portfolio.Api/Controllers/ContactsController.cs`
- Create: `src/Portfolio.Api/Configuration/ContactRateLimitOptions.cs`
- Modify: `src/Portfolio.Api/Extensions/ServiceCollectionExtensions.cs`
- Modify: `src/Portfolio.Api/OpenApi/RequestExampleOperationFilter.cs`
- Modify: `src/Portfolio.Api/appsettings.json`
- Modify: `src/Portfolio.Api/appsettings.Development.json`
- Modify: `.env.example`
- Modify: `tests/Portfolio.IntegrationTests/Api/ApiFoundationTests.cs`
- Test: `tests/Portfolio.IntegrationTests/Api/ContactsApiTests.cs`

**Interfaces:**
- Consumes: Task 1 service contracts, existing `ApiResponse<T>`, `PaginationMeta`, `AdminPolicy`, and ASP.NET Core fixed-window rate limiter.
- Produces: the five Contact routes, `ContactSubmission` rate-limit policy, SHA-256 connection-IP hashing, bounded user-agent metadata, ProblemDetails declarations, and request examples.

- [x] **Step 1: Write failing API tests**

```csharp
[Fact]
public async Task AnonymousPostReturnsReceiptWithoutEchoingPersonalData() { }

[Fact]
public async Task FilledHoneypotReturnsIndistinguishableCreatedResponse() { }

[Fact]
public async Task ExcessRequestsFromOneConnectionReturn429ProblemDetails() { }

[Theory]
[InlineData("/api/v1/admin/contacts")]
[InlineData("/api/v1/admin/contacts/11111111-1111-1111-1111-111111111111")]
public async Task AdminInboxRejectsAnonymousRequests(string path) { }
```

Also extend the global Swagger request-example assertion with `POST /api/v1/contact` and `PATCH /api/v1/admin/contacts/{id}/status`.

- [x] **Step 2: Run the API tests and confirm 404/missing OpenAPI paths**

Run: `dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~ContactsApiTests|FullyQualifiedName~SwaggerEveryJsonRequestBodyHasContractExample`

Expected: FAIL because Contact controllers, rate policy, and examples are absent.

- [x] **Step 3: Implement controllers and Contact limiter**

Add `[EnableRateLimiting("ContactSubmission")]` only to anonymous POST. Configure fixed-window `PermitLimit` and `WindowSeconds` under `RateLimit:Contact`, key by normalized `RemoteIpAddress` or `unknown`, and return generic 429 ProblemDetails with `Retry-After` when supplied. Compute lowercase SHA-256 hex for stored `IpHash`; never read `X-Forwarded-For` directly. Add admin `GET`, detail `GET`, status `PATCH`, and `DELETE` under `AdminPolicy`.

- [x] **Step 4: Run the API tests until green**

Run: `dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~ContactsApiTests|FullyQualifiedName~SwaggerEveryJsonRequestBodyHasContractExample`

Expected: PASS.

### Task 4: Dashboard consistency, documentation, and full verification

**Files:**
- Modify: `tests/Portfolio.IntegrationTests/Persistence/ContactRepositoryTests.cs`
- Modify: `docs/BACKEND_SPEC.md`
- Modify: `docs/security/THREAT_MODEL.md`
- Modify: `docs/testing/TEST_STRATEGY.md`

**Interfaces:**
- Consumes: existing `DashboardRepository` rule `ContactStatus.New` and all completed Contact APIs.
- Produces: regression evidence that status changes and deletion update unread dashboard counts, plus operational configuration/privacy/retention documentation.

- [x] **Step 1: Add a failing dashboard consistency integration test**

```csharp
[Fact]
public async Task DashboardUnreadCountTracksNewStatusAndDeletion()
{
    // Persist New and Read messages, assert one unread, archive/delete New, and assert zero unread.
}
```

- [x] **Step 2: Run the dashboard/contact persistence test and confirm the expected behavior**

Run: `dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~DashboardUnreadCountTracksNewStatusAndDeletion`

Expected: PASS if the existing dashboard query and new repository mutations compose correctly; a failure must be fixed at the responsible repository/service boundary before continuing.

- [x] **Step 3: Update documentation**

Document `RateLimit:Contact:PermitLimit`, `WindowSeconds`, the `website` honeypot, SHA-256 connection-IP treatment, no request-body logging, configured trusted-proxy expectation, no-op notification behavior, and hard-delete/manual-retention MVP behavior.

- [x] **Step 4: Run formatting and full Release verification**

Run: `dotnet format --verify-no-changes`

Run: `dotnet build --configuration Release`

Run: `dotnet test --configuration Release`

Expected: all commands PASS with no warnings. If Docker is unavailable for Testcontainers, report the affected PostgreSQL tests separately rather than claiming they passed.
