# Sprint 4 Work Experience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver bilingual work-experience public reads and administrator CRUD/reorder with validated dates, translations, highlights, and technology links.

**Architecture:** Add an Experience feature following Controller → `IExperienceService` → `ExperienceService` → `IExperienceRepository` → EF Core. The service owns validation and aggregate replacement; the repository owns locale-aware projections, persistence, and serializable full-set reorder.

**Tech Stack:** .NET 10, ASP.NET Core controllers, EF Core 10, Npgsql/PostgreSQL, xUnit.

**Spec:** `docs/superpowers/plans/2026-09-05-personal-portfolio-backend-roadmap.md` Sprint 4, `docs/api/API_CONTRACT.md` section 5, `docs/BACKEND_SPEC.md` BE-04, `docs/database.md` experience tables.

## Global Constraints

- Public reads return published experiences only and only the requested `en` or `vi` translation/highlights.
- `isCurrent` is derived from `endDate == null`; clients do not submit it.
- Publishing requires both English and Vietnamese positions.
- Company URL, when supplied, is an absolute HTTPS URL.
- End date, when supplied, is on or after start date.
- Technology IDs are unique, exist, and preserve request order in `experience_technologies.display_order`.
- Highlight orders are non-negative and unique within each locale/highlight-type pair.
- Reorder accepts the complete current experience set and commits atomically.
- No database migration is expected because the Sprint 1 schema already contains all experience tables, constraints, foreign keys, and indexes.

---

### Task 1: Define and test application behavior

**Files:**
- Create: `tests/Portfolio.UnitTests/Experiences/ExperienceServiceTests.cs`
- Create: `src/Portfolio.Application/Experiences/ExperienceContracts.cs`
- Create: `src/Portfolio.Application/Experiences/IExperienceService.cs`
- Create: `src/Portfolio.Application/Experiences/IExperienceRepository.cs`
- Create: `src/Portfolio.Application/Experiences/ExperienceService.cs`
- Modify: `src/Portfolio.Application/DependencyInjection.cs`

**Interfaces:**
- Consumes: `WorkExperience`, `ExperienceTranslation`, `ExperienceHighlight`, `ExperienceTechnology`, `SupportedLocales`, application exceptions.
- Produces: `IExperienceService.GetPublicAsync`, `GetExperiencesAsync`, `GetExperienceAsync`, `CreateExperienceAsync`, `UpdateExperienceAsync`, `DeleteExperienceAsync`, and `ReorderExperiencesAsync`.

- [ ] **Step 1: Write failing service tests**

```csharp
[Fact] public async Task CreateRejectsEndBeforeStart();
[Fact] public async Task PublishingRequiresBothTranslations();
[Fact] public async Task CreateRejectsUnknownTechnology();
[Fact] public async Task CreateRejectsDuplicateHighlightOrderWithinLocaleAndType();
[Fact] public async Task CreateDerivesTechnologyDisplayOrderFromRequestOrder();
[Fact] public async Task ReorderRejectsDuplicateIds();
```

- [ ] **Step 2: Run the new unit tests and verify RED**

```powershell
dotnet test tests/Portfolio.UnitTests/Portfolio.UnitTests.csproj --configuration Release --filter FullyQualifiedName~ExperienceServiceTests
```

Expected: compilation/test failure because the Experience application contracts and service do not exist.

- [ ] **Step 3: Implement the contracts and minimal service behavior**

```csharp
public interface IExperienceService
{
    Task<IReadOnlyList<PublicExperienceResponse>> GetPublicAsync(string slug, string? locale, CancellationToken cancellationToken);
    Task<ExperienceAdminPage> GetExperiencesAsync(ExperienceAdminQuery query, CancellationToken cancellationToken);
    Task<ExperienceAdminResponse> GetExperienceAsync(Guid id, CancellationToken cancellationToken);
    Task<ExperienceAdminResponse> CreateExperienceAsync(ExperienceWriteRequest request, CancellationToken cancellationToken);
    Task<ExperienceAdminResponse> UpdateExperienceAsync(Guid id, ExperienceWriteRequest request, CancellationToken cancellationToken);
    Task DeleteExperienceAsync(Guid id, CancellationToken cancellationToken);
    Task ReorderExperiencesAsync(ExperienceReorderRequest request, CancellationToken cancellationToken);
}
```

Validate all documented lengths, URL/date rules, locale/type/order uniqueness, complete publish translations, pagination, and technology existence before mutating the aggregate. Register `IExperienceService` with `ExperienceService`.

- [ ] **Step 4: Run the focused unit tests and verify GREEN**

```powershell
dotnet test tests/Portfolio.UnitTests/Portfolio.UnitTests.csproj --configuration Release --filter FullyQualifiedName~ExperienceServiceTests
```

- [ ] **Step 5: Review the service mutation coverage**

Confirm a test fails for removing each validation branch, omitting aggregate child replacement, or assigning every linked technology the same order.

### Task 2: Implement and test PostgreSQL persistence

**Files:**
- Create: `tests/Portfolio.IntegrationTests/Persistence/ExperienceRepositoryTests.cs`
- Create: `src/Portfolio.Infrastructure/Persistence/Repositories/ExperienceRepository.cs`
- Create: `src/Portfolio.Infrastructure/Availability/DatabaseUnavailableExperienceRepository.cs`
- Modify: `src/Portfolio.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `IExperienceRepository` and Experience DTO/projection records from Task 1.
- Produces: locale-aware public projection, filtered/paginated admin reads, tracked aggregate loading, technology existence checks, save/delete, and serializable reorder.

- [ ] **Step 1: Write failing repository integration tests**

```csharp
[Fact] public async Task PublicListFiltersDraftsAndOrdersByDisplayOrderThenStartDateThenId();
[Fact] public async Task PublicListReturnsRequestedLocaleAndPublishedTechnologiesOnly();
[Fact] public async Task AdminListSearchesCompanyAndLocalizedPosition();
[Fact] public async Task AggregateSavePersistsTranslationsHighlightsAndOrderedTechnologies();
[Fact] public async Task ReorderRejectsPartialSetWithoutChangingOrders();
```

- [ ] **Step 2: Run the repository tests and verify RED**

```powershell
dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~ExperienceRepositoryTests
```

Expected: compilation/test failure because `ExperienceRepository` is absent.

- [ ] **Step 3: Implement the repository and availability fallback**

Use `AsNoTracking` for reads; filter the selected locale and published technology/technology translation in the public query; order by `DisplayOrder`, `StartDate DESC`, then `Id`; load all children for tracked aggregate replacement; and use a serializable transaction for complete-set reorder.

- [ ] **Step 4: Register configured and unavailable repository implementations**

```csharp
services.AddScoped<IExperienceRepository, ExperienceRepository>();
// or DatabaseUnavailableExperienceRepository when PostgreSQL is unconfigured
```

- [ ] **Step 5: Run the focused repository tests and verify GREEN**

```powershell
dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~ExperienceRepositoryTests
```

### Task 3: Expose and test HTTP routes

**Files:**
- Create: `tests/Portfolio.IntegrationTests/Api/ExperiencesApiTests.cs`
- Create: `src/Portfolio.Api/Controllers/ExperiencesController.cs`

**Interfaces:**
- Consumes: `IExperienceService` from Task 1 and the existing `ApiResponse<T>`, pagination metadata, Admin policy, and global ProblemDetails mapping.
- Produces: all seven routes in API contract section 5.

- [ ] **Step 1: Write failing API boundary tests**

```csharp
[Fact] public async Task SwaggerContainsEveryExperienceRoute();
[Theory] public async Task AdminExperienceRoutesRejectAnonymousRequests(string path);
[Fact] public async Task AdminExperienceRoutesRejectNonAdminToken();
[Fact] public async Task UnsupportedExperienceLocaleReturnsProblemDetailsBeforeDatabaseAccess();
```

- [ ] **Step 2: Run the API tests and verify RED**

```powershell
dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~ExperiencesApiTests
```

Expected: assertions fail because Experience routes are absent.

- [ ] **Step 3: Implement public and administrator controllers**

```text
GET    /api/v1/portfolio/{slug}/experiences
GET    /api/v1/admin/experiences
GET    /api/v1/admin/experiences/{id}
POST   /api/v1/admin/experiences
PUT    /api/v1/admin/experiences/{id}
DELETE /api/v1/admin/experiences/{id}
PATCH  /api/v1/admin/experiences/reorder
```

Return 201 with `CreatedAtAction` for create, 204 for delete, paginated metadata for admin list, and the submitted reorder result for a successful reorder.

- [ ] **Step 4: Run the focused API tests and verify GREEN**

```powershell
dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~ExperiencesApiTests
```

### Task 4: Full verification and contract review

**Files:**
- Modify only files required by failures attributable to Sprint 4.

**Interfaces:**
- Consumes: completed Tasks 1-3.
- Produces: verified Sprint 4 implementation.

- [ ] **Step 1: Build Release**

```powershell
dotnet build Portfolio.sln --configuration Release
```

- [ ] **Step 2: Run all tests**

```powershell
dotnet test Portfolio.sln --configuration Release --no-build
```

- [ ] **Step 3: Verify formatting**

```powershell
dotnet format Portfolio.sln --verify-no-changes --no-restore
```

- [ ] **Step 4: Inspect schema impact**

Confirm no migration or model-snapshot diff exists and existing experience constraints/indexes remain unchanged.

- [ ] **Step 5: Re-read Sprint 4 and API contract section 5**

Confirm public draft filtering, locale projection, derived `isCurrent`, CRUD, search/pagination, aggregate child replacement, technology validation/order, structured logs, authorization, and atomic full-set reorder each have implementation and test evidence.
