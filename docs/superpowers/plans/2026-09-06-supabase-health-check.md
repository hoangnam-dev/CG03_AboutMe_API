# Supabase Health Check Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a safe `/health/supabase` endpoint that reports PostgreSQL and Supabase Storage connectivity without exposing credentials or provider errors.

**Architecture:** Keep health checks in the hosting/infrastructure boundary. Reuse EF Core's DbContext health check for PostgreSQL, add a read-only Storage API probe that lists and validates configured buckets, and serialize only component names, status, and duration from the health report.

**Tech Stack:** .NET 10, ASP.NET Core Health Checks, EF Core, Npgsql, Supabase Storage HTTP API, xUnit v3.

**Spec:** `docs/BACKEND_SPEC.md`, `docs/security/THREAT_MODEL.md`, `docs/testing/TEST_STRATEGY.md`, `docs/STORAGE.md`

## Global Constraints

- `/health` remains dependency-free liveness.
- `/health/ready` continues to represent dependency readiness.
- `/health/supabase` checks both PostgreSQL and Supabase Storage.
- Storage probing is read-only and never uploads or deletes an object.
- Responses never include exception messages, response bodies, URLs, connection strings, keys, or tokens.
- Ordinary tests do not require live Supabase credentials.
- No database migration or schema change is required.

---

### Task 1: Define the Storage health-check contract test-first

**Files:**
- Create: `tests/Portfolio.IntegrationTests/Storage/SupabaseStorageHealthCheckTests.cs`
- Create: `src/Portfolio.Infrastructure/Storage/SupabaseStorageHealthCheck.cs`
- Modify: `Directory.Packages.props`
- Modify: `src/Portfolio.Infrastructure/Portfolio.Infrastructure.csproj`

**Interfaces:**
- Consumes: `SupabaseStorageOptions`, configured `HttpClient`.
- Produces: `SupabaseStorageHealthCheck : IHealthCheck`.

- [x] **Step 1: Write failing contract tests**

```csharp
[Fact]
public async Task CheckHealthReturnsHealthyWhenAllConfiguredBucketsExist()
{
    var handler = new RecordingHandler(_ => JsonResponse(
        """[{"id":"avatars"},{"id":"project-images"},{"id":"certificate-files"},{"id":"cv-files"}]"""));
    var check = CreateHealthCheck(handler);

    var result = await check.CheckHealthAsync(
        new HealthCheckContext(),
        TestContext.Current.CancellationToken);

    Assert.Equal(HealthStatus.Healthy, result.Status);
    Assert.Equal(HttpMethod.Get, handler.Request!.Method);
    Assert.Equal("/storage/v1/bucket", handler.Request.RequestUri!.AbsolutePath);
}
```

Add focused cases for a missing configured bucket, provider failure, malformed/oversized JSON, and timeout. Assert only a generic safe health description.

- [x] **Step 2: Run the focused test and confirm RED**

```powershell
dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~SupabaseStorageHealthCheckTests
```

Expected: compilation failure because `SupabaseStorageHealthCheck` does not exist.

- [x] **Step 3: Add the minimal health-check implementation**

Implement `CheckHealthAsync` to send authenticated `GET storage/v1/bucket`, bound the response to `MaxResponseBytes`, parse bucket IDs, and return `Healthy` only when every configured bucket exists. Translate timeout, transport, status, size, and JSON failures to a generic `Unhealthy` result.

- [x] **Step 4: Run the focused test and confirm GREEN**

```powershell
dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~SupabaseStorageHealthCheckTests
```

Expected: all `SupabaseStorageHealthCheckTests` pass.

### Task 2: Register tagged Supabase checks and expose safe JSON

**Files:**
- Create: `src/Portfolio.Api/Health/HealthCheckResponseWriter.cs`
- Modify: `src/Portfolio.Infrastructure/DependencyInjection.cs`
- Modify: `src/Portfolio.Api/Extensions/ServiceCollectionExtensions.cs`
- Modify: `src/Portfolio.Api/Program.cs`
- Modify: `tests/Portfolio.IntegrationTests/Api/ApiFoundationTests.cs`

**Interfaces:**
- Consumes: health registrations tagged `supabase`.
- Produces: `GET /health/supabase`, JSON `{ status, totalDurationMs, checks }`.

- [x] **Step 1: Write the failing API test**

```csharp
[Fact]
public async Task SupabaseHealthEndpointReturnsSafeComponentStatus()
{
    await using var factory = new DatabaseOptionalApiFactory();
    using var client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    var response = await client.GetAsync(
        "/health/supabase",
        TestContext.Current.CancellationToken);
    var payload = await response.Content.ReadFromJsonAsync<JsonElement>(
        TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    Assert.Equal("unhealthy", payload.GetProperty("status").GetString());
    Assert.True(payload.GetProperty("checks").TryGetProperty("postgresql", out _));
    Assert.True(payload.GetProperty("checks").TryGetProperty("supabase-storage", out _));
    Assert.DoesNotContain("Password", payload.ToString(), StringComparison.OrdinalIgnoreCase);
}
```

- [x] **Step 2: Run the API test and confirm RED**

```powershell
dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~SupabaseHealthEndpointReturnsSafeComponentStatus
```

Expected: HTTP 404 because the route does not exist.

- [x] **Step 3: Implement registration and endpoint**

Register PostgreSQL and Storage checks with both `ready` and `supabase` tags. Register deterministic unhealthy checks when either dependency is unconfigured. Add `/health/supabase` with predicate `registration.Tags.Contains("supabase")` and the safe JSON response writer.

- [x] **Step 4: Run the API test and confirm GREEN**

```powershell
dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~SupabaseHealthEndpointReturnsSafeComponentStatus
```

Expected: the endpoint returns 503 JSON with both safe component entries when dependencies are unconfigured.

Implementation note: the initial API test established the expected 404 RED, but the repository's minimal-host test factory loaded developer User Secrets before its override and contacted live Supabase. The final deterministic coverage therefore tests the response writer directly and keeps Storage probing under a fake `HttpMessageHandler`; ordinary tests do not depend on live credentials.

### Task 3: Document and verify

**Files:**
- Modify: `docs/LOCAL_SETUP.md`

**Interfaces:**
- Produces: local commands and response contract for `/health/supabase`.

- [x] **Step 1: Document endpoint semantics**

Document `/health`, `/health/ready`, and `/health/supabase`, including the fact that the Supabase endpoint performs live dependency calls and may return 503 during outage or cold start.

- [ ] **Step 2: Run verification**

```powershell
dotnet build --configuration Release
dotnet test --configuration Release
dotnet format --verify-no-changes
```

Expected: build succeeds, all tests pass, and formatting reports no changes required.

Verification status: build, 37 unit tests, five focused health tests, and scoped formatting passed. The full integration suite ran 57 tests; 52 passed and five PostgreSQL/Testcontainers tests could not start because Docker was unavailable. Repository-wide formatting remains blocked by pre-existing LF/CRLF findings outside this feature.

- [x] **Step 3: Review security and scope**

Confirm the response contains no descriptions/exceptions/provider payloads, the Storage operation is read-only, no migration exists, and unrelated working-tree changes remain untouched.
