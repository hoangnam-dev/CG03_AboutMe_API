# Sprint 7 Resume Versioning and Current CV Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver current CV access and concurrency-safe English/Vietnamese Resume version administration without overwriting historical files.

**Architecture:** Add a Resumes feature following Controller -> `IResumeService` -> `ResumeService` -> `IResumeRepository` -> EF Core/PostgreSQL. The service owns request validation, Asia/Ho_Chi_Minh year selection, immutable storage upload/compensation, version formatting, state rules, and signed access; the repository owns public/admin queries and transactionally allocates counters, inserts metadata, and switches the active Resume.

**Tech Stack:** .NET 10, ASP.NET Core controller APIs, EF Core 10, Npgsql/PostgreSQL, Supabase Storage abstraction, xUnit, Testcontainers.

**Spec:** `docs/superpowers/plans/2026-09-05-personal-portfolio-backend-roadmap.md` Sprint 7; `docs/api/API_CONTRACT.md` section 8; `docs/BACKEND_SPEC.md` BE-07; `docs/database.md` sections 3 and 5; `docs/security/THREAT_MODEL.md`; `docs/testing/TEST_STRATEGY.md`.

## Global Constraints

- `language` is required and must be exactly `en` or `vi`.
- Resume files are non-empty PDF files whose extension, declared MIME, and `%PDF-` signature agree, bounded by `Upload:MaxFileSize`.
- Clients never supply `version`, `versionYear`, or `versionSequence`; display versions are server-computed as `v{year}_{sequence:00}`.
- The allocation year comes from `Asia/Ho_Chi_Minh` through the injected `TimeProvider`.
- Counter allocation, Resume insert, and optional active switch share one PostgreSQL transaction using `INSERT ... ON CONFLICT ... DO UPDATE ... RETURNING`.
- At most one active Resume exists per language, and only a published Resume may become active.
- Uploads create immutable historical rows and generated `resumes/{resumeId}/{uuid}.pdf` private object keys; they never overwrite an existing object.
- A database failure after upload compensates the new object without changing the old active Resume.
- Public reads return only a published active Resume for the requested language and expose a five-minute signed URL, never a raw object key.
- Active versions cannot be unpublished or deleted; deletion commits metadata before best-effort object cleanup.
- No schema migration is expected because Sprint 1 already created the counter FK, alternate key, checks, and partial unique active index.

---

### Task 1: Define Resume application behavior test-first

**Files:**
- Create: `tests/Portfolio.UnitTests/Resumes/ResumeServiceTests.cs`
- Create: `src/Portfolio.Application/Resumes/ResumeContracts.cs`
- Create: `src/Portfolio.Application/Resumes/IResumeService.cs`
- Create: `src/Portfolio.Application/Resumes/IResumeRepository.cs`
- Create: `src/Portfolio.Application/Resumes/ResumeVersionFormatter.cs`
- Create: `src/Portfolio.Application/Resumes/ResumeService.cs`
- Modify: `src/Portfolio.Application/DependencyInjection.cs`

**Interfaces:**
- Consumes: `ResumeFile`, `ResumeTranslation`, `IFileStorage`, `FileValidation`, `TimeProvider`, application exceptions.
- Produces: public current read, paged admin list, upload, set-current, publish, and delete use cases.

- [x] **Step 1:** Write failing tests for required language, zero-byte/non-PDF rejection, active-requires-published, Asia/Ho_Chi_Minh year allocation, generated versions, upload compensation, signed public access, set-current without a new version, active unpublish conflict, and active delete conflict.
- [x] **Step 2:** Run `dotnet test tests/Portfolio.UnitTests/Portfolio.UnitTests.csproj --configuration Release --filter FullyQualifiedName~Resume` and verify failures are caused by missing Resume contracts/behavior.
- [x] **Step 3:** Implement the minimal contracts, formatter, service, and DI registration needed by the tests. Use `FileValidationOptions.Pdf`, generate `resumes/{resumeId}/{uuid}.pdf`, and use five-minute signed URLs.
- [x] **Step 4:** Re-run the focused unit tests and require zero failures.

### Task 2: Implement transactional PostgreSQL persistence test-first

**Files:**
- Create: `tests/Portfolio.IntegrationTests/Persistence/ResumeRepositoryTests.cs`
- Create: `src/Portfolio.Infrastructure/Persistence/Repositories/ResumeRepository.cs`
- Create: `src/Portfolio.Infrastructure/Availability/DatabaseUnavailableResumeRepository.cs`
- Modify: `src/Portfolio.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: application Resume repository contracts and `PortfolioDbContext`.
- Produces: projected public/admin reads plus transaction-owning create-and-activate and set-current operations.

- [x] **Step 1:** Write failing PostgreSQL tests proving independent EN/VI counters, unique monotonic concurrent allocation, exact public filtering, rollback of counter/metadata/active switch after injected persistence failure, and one-active-per-language switching.
- [x] **Step 2:** Run `dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~ResumeRepositoryTests` and verify failures are caused by the missing repository.
- [x] **Step 3:** Implement parameterized counter upsert with `RETURNING`, Resume insert and optional `ExecuteUpdateAsync` active switch inside one EF transaction; add ordered no-tracking queries and the unavailable-database adapter.
- [x] **Step 4:** Re-run the focused repository tests and require zero failures.

### Task 3: Expose and verify Resume HTTP contracts

**Files:**
- Create: `tests/Portfolio.IntegrationTests/Api/ResumesApiTests.cs`
- Create: `src/Portfolio.Api/Controllers/ResumesController.cs`
- Modify: `src/Portfolio.Api/Extensions/ServiceCollectionExtensions.cs`
- Modify: `tests/Portfolio.IntegrationTests/Api/ApiFoundationTests.cs`
- Modify: `docs/STORAGE.md`

**Interfaces:**
- Consumes: `IResumeService`, `ApiResponse<T>`, Admin authorization policy, upload/storage settings.
- Produces: `GET /api/v1/portfolio/{slug}/cv/current`, `GET|POST /api/v1/admin/cv`, `PATCH /api/v1/admin/cv/{id}/current`, `PATCH /api/v1/admin/cv/{id}/publish`, and `DELETE /api/v1/admin/cv/{id}`.

- [x] **Step 1:** Write failing API tests for Swagger route presence, anonymous/non-admin rejection, unsupported/missing public language ProblemDetails, signed response serialization without object keys, and rejection of multipart client version fields.
- [x] **Step 2:** Run `dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~ResumesApiTests` and verify failures are caused by missing routes.
- [x] **Step 3:** Add thin public/admin controllers, explicit multipart version-field rejection, Resume settings registration using `SupabaseStorage:Buckets:CvFiles`, and request examples for JSON PATCH bodies.
- [x] **Step 4:** Document Resume key/version/transaction/deletion behavior in `docs/STORAGE.md`, then re-run the focused API tests.

### Task 4: Verify the complete Sprint 7 slice

**Files:**
- Inspect: all files changed by Tasks 1-3 and EF migration/model snapshot files.

**Interfaces:**
- Consumes: the complete Sprint 7 implementation.
- Produces: executable evidence for the Definition of Done.

- [x] **Step 1:** Run `dotnet build Portfolio.sln --configuration Release`.
- [x] **Step 2:** Run `dotnet test Portfolio.sln --configuration Release --no-build`.
- [x] **Step 3:** Run `dotnet format Portfolio.sln --verify-no-changes --no-restore --include src/Portfolio.Application/Resumes src/Portfolio.Infrastructure/Persistence/Repositories/ResumeRepository.cs src/Portfolio.Infrastructure/Availability/DatabaseUnavailableResumeRepository.cs src/Portfolio.Api/Controllers/ResumesController.cs tests/Portfolio.UnitTests/Resumes tests/Portfolio.IntegrationTests/Persistence/ResumeRepositoryTests.cs tests/Portfolio.IntegrationTests/Api/ResumesApiTests.cs`; the repository-wide check remains blocked by pre-existing formatting findings outside Sprint 7.
- [x] **Step 4:** Confirm no migration/model snapshot change and re-check every Global Constraint, the relevant storage-compensation regression rule, and external-dependency isolation in API factories.
