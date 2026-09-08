# Sprint 5 Projects and Gallery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver bilingual public project list/detail endpoints and administrator project/gallery management without draft or limited-disclosure leaks.

**Architecture:** Add a Projects feature following Controller → `IProjectService` → `ProjectService` → `IProjectRepository` → EF Core. Business validation, disclosure-safe response mapping, and storage compensation stay in the service; PostgreSQL projections, aggregate persistence, and atomic reorder stay in the repository.

**Tech Stack:** .NET 10, ASP.NET Core controllers, EF Core 10, Npgsql/PostgreSQL, Supabase Storage abstraction, xUnit.

**Spec:** `docs/superpowers/plans/2026-09-05-personal-portfolio-backend-roadmap.md` Sprint 5; `docs/api/API_CONTRACT.md` section 6; `docs/BACKEND_SPEC.md` BE-05; `docs/database.md` project tables; `docs/security/THREAT_MODEL.md` disclosure and storage controls.

## Global Constraints

- Public reads return only published projects and only the requested `en` or `vi` translation.
- Limited responses omit repository/demo URLs, gallery images, client context, problem, solution, and result at serialization time.
- Project slugs are normalized and globally unique for the single-portfolio MVP.
- Publishing requires both localized names and both alt texts for every gallery image.
- Technology IDs, highlight orders, image orders, and upload `fileIndex` values are validated for uniqueness and existence.
- Gallery uploads accept PNG/JPEG/WebP only, generate keys under `projects/{projectId}/{uuid}.{extension}`, and never persist client paths.
- A failed gallery upload or database commit compensates all new objects; old objects are deleted only after metadata is committed.
- Reorder requires the complete current project ID set and commits atomically.
- No database migration is expected; the existing schema already contains project aggregates, cascades, constraints, and public-order index.

---

### Task 1: Define and test project application behavior

**Files:**
- Create: `tests/Portfolio.UnitTests/Projects/ProjectServiceTests.cs`
- Create: `src/Portfolio.Application/Projects/ProjectContracts.cs`
- Create: `src/Portfolio.Application/Projects/IProjectService.cs`
- Create: `src/Portfolio.Application/Projects/IProjectRepository.cs`
- Create: `src/Portfolio.Application/Projects/ProjectService.cs`
- Modify: `src/Portfolio.Application/DependencyInjection.cs`

**Interfaces:**
- Consumes: existing Project aggregate models, localization/slug/file validators, `IFileStorage`, and application exceptions.
- Produces: public list/detail, admin page/detail, CRUD, publish, reorder, and multi-file gallery upload service operations.

- [ ] **Step 1: Write failing disclosure, validation, and compensation tests**

```csharp
[Fact] public async Task GetLimitedDetailOmitsSensitiveFields();
[Fact] public async Task CreateRejectsDuplicateNormalizedSlug();
[Fact] public async Task PublishRequiresBothTranslationsAndEveryImageAltText();
[Fact] public async Task UploadGalleryCompensatesEveryNewObjectWhenUploadFails();
[Fact] public async Task UploadGalleryCompensatesEveryNewObjectWhenSaveFails();
[Fact] public async Task ReorderRejectsDuplicateIds();
```

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
dotnet test tests/Portfolio.UnitTests/Portfolio.UnitTests.csproj --configuration Release --filter FullyQualifiedName~ProjectServiceTests
```

Expected: compilation failure because the Projects application boundary is absent.

- [ ] **Step 3: Implement the minimal contracts and service**

Validate field limits, HTTPS URLs, dates, locale dictionaries, child ordering, technology existence, thumbnail ownership, and publish completeness before mutation. Map full and limited public detail through separate public DTOs and apply `JsonIgnore(WhenWritingNull)` to disclosure-dependent properties.

- [ ] **Step 4: Run focused tests and verify GREEN**

```powershell
dotnet test tests/Portfolio.UnitTests/Portfolio.UnitTests.csproj --configuration Release --filter FullyQualifiedName~ProjectServiceTests
```

### Task 2: Implement and test PostgreSQL persistence

**Files:**
- Create: `tests/Portfolio.IntegrationTests/Persistence/ProjectRepositoryTests.cs`
- Create: `src/Portfolio.Infrastructure/Persistence/Repositories/ProjectRepository.cs`
- Create: `src/Portfolio.Infrastructure/Availability/DatabaseUnavailableProjectRepository.cs`
- Modify: `src/Portfolio.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `IProjectRepository` and Project projection/ordering records.
- Produces: bounded locale-aware public/admin reads, tracked aggregate loading, normalized-slug/technology checks, save/delete, and serializable complete-set reorder.

- [ ] **Step 1: Write failing repository integration tests**

```csharp
[Fact] public async Task PublicListFiltersDraftsAndOrdersFeaturedThenDisplayOrderThenId();
[Fact] public async Task LimitedDetailDoesNotProjectSensitiveData();
[Fact] public async Task AggregateSavePersistsTranslationsHighlightsImagesAndTechnologies();
[Fact] public async Task ReorderRejectsPartialSetWithoutChangingOrders();
```

- [ ] **Step 2: Run repository tests and verify RED**

```powershell
dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~ProjectRepositoryTests
```

- [ ] **Step 3: Implement repository and unavailable fallback**

Use `AsNoTracking`, exact-locale predicates, published technology filtering, deterministic ordering, complete tracked aggregate includes, and a serializable transaction for reorder. Register configured and unavailable implementations.

- [ ] **Step 4: Run focused repository tests and verify GREEN**

```powershell
dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~ProjectRepositoryTests
```

### Task 3: Expose and test HTTP routes

**Files:**
- Create: `tests/Portfolio.IntegrationTests/Api/ProjectsApiTests.cs`
- Create: `src/Portfolio.Api/Controllers/ProjectsController.cs`
- Modify: `src/Portfolio.Api/Configuration/UploadOptions.cs`
- Modify: `src/Portfolio.Api/Configuration/UploadOptionsValidator.cs`
- Modify: `src/Portfolio.Api/Extensions/ServiceCollectionExtensions.cs`
- Modify: `src/Portfolio.Api/appsettings.json`

**Interfaces:**
- Consumes: `IProjectService`, existing response envelopes, Admin policy, ProblemDetails, and upload/storage options.
- Produces: the ten routes in API contract section 6 and the multipart `files` plus JSON `metadata` gallery boundary.

- [ ] **Step 1: Write failing OpenAPI, authorization, locale, disclosure, and multipart tests**

```csharp
[Fact] public async Task SwaggerContainsEveryProjectRoute();
[Theory] public async Task AdminProjectRoutesRejectAnonymousRequests(string path);
[Fact] public async Task UnsupportedProjectLocaleReturnsProblemDetailsBeforeDatabaseAccess();
[Fact] public async Task LimitedDetailJsonOmitsEverySensitiveProperty();
[Fact] public async Task InvalidGalleryMetadataReturnsProblemDetails();
```

- [ ] **Step 2: Run API tests and verify RED**

```powershell
dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~ProjectsApiTests
```

- [ ] **Step 3: Implement public/admin controllers and bounded gallery binding**

Expose public list/detail, admin list/detail/create/update/delete/publish/reorder, and gallery upload. Deserialize metadata with web JSON options, keep file streams scoped to the request, and return RFC ProblemDetails through centralized application exceptions.

- [ ] **Step 4: Run API tests and verify GREEN**

```powershell
dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~ProjectsApiTests
```

### Task 4: Full verification and contract review

**Files:**
- Modify only files required by failures attributable to Sprint 5.

**Interfaces:**
- Consumes: completed Tasks 1-3.
- Produces: verified Sprint 5 implementation and unchanged EF schema.

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

- [ ] **Step 4: Inspect schema and SQL impact**

Confirm no migration/model-snapshot change, public queries remain bounded and use the existing `idx_projects_public_filter_order`, and project cascades/FKs are unchanged.

- [ ] **Step 5: Re-read Sprint 5 and the disclosure matrix**

Confirm draft filtering, exact locale, full/limited property omission, CRUD/publish/reorder, aggregate replacement, gallery validation/compensation, post-commit cleanup, authorization, OpenAPI, and relevant regression coverage.
