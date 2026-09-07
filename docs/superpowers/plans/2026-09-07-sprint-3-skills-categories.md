# Sprint 3 Skills and Categories Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver ordered bilingual skill categories and technologies with public projection, secure administrator CRUD, validated icons, restricted deletes, and atomic reorder.

**Architecture:** Add a `Skills` application feature whose service owns validation and state rules and whose feature-specific repository owns EF Core projections and persistence. Thin public/admin controllers expose the approved routes. Existing skill tables and constraints are reused; a configurable public `skill-icons` Storage bucket closes the approved upload-contract gap without a schema change.

**Tech Stack:** .NET 10, ASP.NET Core Web API, EF Core 10, Npgsql/PostgreSQL, Supabase Storage, xUnit.

**Spec:** `docs/superpowers/plans/2026-09-05-personal-portfolio-backend-roadmap.md` Sprint 3, `docs/api/API_CONTRACT.md` section 4, `docs/BACKEND_SPEC.md` BE-03.

## Global Constraints

- Preserve `Controller -> IService -> Service -> IRepository -> Repository -> PortfolioDbContext`.
- Public reads return only published categories and published technologies in the requested `en|vi` locale, ordered by `displayOrder`, then `id` in SQL.
- Administrator mutations require `AdminPolicy`; new records default to Draft unless explicitly published with complete `en` and `vi` translations.
- Technology level is exactly `Primary|Experienced|Familiar|Learning`; icon type is exactly `lucide|image|text`.
- Category deletion conflicts while technologies remain; technology deletion conflicts while Experience, Project, or Certificate references remain.
- Reorder is a full-set category operation validated and committed atomically.
- Client filenames and paths never determine Storage object keys; replacement uploads compensate when persistence fails.
- No database migration is expected.

---

### Task 1: Contracts, validation, and service business rules

**Files:**
- Create: `src/Portfolio.Application/Skills/SkillContracts.cs`
- Create: `src/Portfolio.Application/Skills/ISkillService.cs`
- Create: `src/Portfolio.Application/Skills/ISkillRepository.cs`
- Create: `src/Portfolio.Application/Skills/SkillIconValidator.cs`
- Create: `src/Portfolio.Application/Skills/SkillService.cs`
- Modify: `src/Portfolio.Application/DependencyInjection.cs`
- Test: `tests/Portfolio.UnitTests/Skills/SkillIconValidatorTests.cs`
- Test: `tests/Portfolio.UnitTests/Skills/SkillServiceTests.cs`

**Interfaces:**
- Produces: `ISkillService` public/admin reads, category/technology CRUD, reorder, and icon replacement methods.
- Produces: `ISkillRepository` projections, duplicate/reference checks, tracked aggregate access, persistence, and atomic reorder result.
- Produces: request/response records matching API section 4 and a fixed Lucide allowlist: `atom`, `blocks`, `boxes`, `code-xml`, `database`, `database-zap`, `git-branch`, `layers`, `panel-top`, `triangle`, `wind`.

- [ ] Write focused tests that fail because the Skills service and validator do not exist: invalid level, names, publish completeness, unpublished parent, duplicate localized name within category, restricted deletes, duplicate reorder IDs/orders, invalid text/Lucide/image icon values, and upload persistence compensation.
- [ ] Run `dotnet test tests/Portfolio.UnitTests/Portfolio.UnitTests.csproj --configuration Release --filter FullyQualifiedName~Skills` and confirm compile/test failure is caused by missing Sprint 3 types.
- [ ] Implement the minimal contracts, validator, and service behavior. Normalize names/icon values, reject unknown locales and unsafe image URLs, map entities to explicit response DTOs, and emit structured administrator-operation logs.
- [ ] Register `ISkillService` and run the focused unit tests green.

### Task 2: EF Core repository and database behavior

**Files:**
- Create: `src/Portfolio.Infrastructure/Persistence/Repositories/SkillRepository.cs`
- Create: `src/Portfolio.Infrastructure/Availability/DatabaseUnavailableSkillRepository.cs`
- Modify: `src/Portfolio.Infrastructure/DependencyInjection.cs`
- Test: `tests/Portfolio.IntegrationTests/Persistence/SkillRepositoryTests.cs`
- Test: `tests/Portfolio.IntegrationTests/Persistence/PersistenceConstraintTests.cs`

**Interfaces:**
- Consumes: `ISkillRepository`, projections, filters, and `SkillReorderResult` from Task 1.
- Produces: translated public grouping; administrator category/list/detail queries; case-insensitive duplicate checks scoped by category and locale; reference checks; tracked CRUD; serializable full-set reorder.

- [ ] Write PostgreSQL tests for published category/technology/locale filtering and SQL ordering, duplicate translation protection, FK delete restriction, admin search/filter/pagination, reference detection, and rollback when reorder is not a full set.
- [ ] Run the focused persistence tests and confirm they fail because the repository is absent.
- [ ] Implement EF Core projection/query methods with `AsNoTracking`, SQL filtering/ordering, and cancellation propagation; implement reorder inside an explicit serializable transaction.
- [ ] Register the real repository when PostgreSQL is configured and the explicit 503 fallback otherwise; run focused persistence tests green.

### Task 3: HTTP routes, authorization, and pagination

**Files:**
- Create: `src/Portfolio.Api/Controllers/SkillsController.cs`
- Modify: `src/Portfolio.Api/Extensions/ServiceCollectionExtensions.cs`
- Test: `tests/Portfolio.IntegrationTests/Api/SkillsApiTests.cs`

**Interfaces:**
- Consumes: `ISkillService`, `ApiResponse<T>`, `PaginationMeta`, and `AdminPolicy`.
- Produces: all section-4 public/admin category and skill routes with 200/201/204 responses and centralized Problem Details failures.

- [ ] Write API tests for Swagger route coverage, anonymous public access, anonymous administrator 401, non-admin 403, unsupported locale 400, malformed payload 400, and pagination metadata.
- [ ] Run focused API tests and confirm routes are absent.
- [ ] Add thin public and administrator controllers, explicit response annotations, `CreatedAtAction` locations, query defaults (`page=1`, `pageSize=20`), and multipart binding.
- [ ] Run focused API tests green.

### Task 4: Skill icon upload and storage contract

**Files:**
- Modify: `src/Portfolio.Application/Common/Validation/FileUpload.cs`
- Modify: `src/Portfolio.Application/Common/Validation/FileValidation.cs`
- Create: `src/Portfolio.Application/Common/Validation/SvgValidation.cs`
- Create: `src/Portfolio.Api/Configuration/SkillIconOptions.cs`
- Create: `src/Portfolio.Api/Configuration/SkillIconOptionsValidator.cs`
- Modify: `src/Portfolio.Api/Extensions/ServiceCollectionExtensions.cs`
- Modify: `src/Portfolio.Infrastructure/Storage/SupabaseStorageOptions.cs`
- Modify: `src/Portfolio.Api/appsettings.json`
- Test: `tests/Portfolio.UnitTests/Common/Validation/FileValidationTests.cs`
- Test: `tests/Portfolio.UnitTests/Skills/SkillServiceTests.cs`

**Interfaces:**
- Produces: `SkillIconSettings(Bucket, MaxFileSize, AllowedExternalHosts)`, SVG as a validated upload kind, and strict passive-SVG validation.
- Consumes: `IFileStorage` and `StorageReplacement` to replace `skills/{technologyId}/{uuid}.{ext}` safely.

- [ ] Write failing tests for valid passive SVG and rejection of script, event-handler, external reference, DTD/entity, and malformed SVG; add replacement-order/compensation tests.
- [ ] Run focused validation/service tests and observe the expected failures.
- [ ] Implement bounded SVG parsing with DTD prohibited and allowlisted elements/attributes, add the `skill-icons` public bucket and size/host options, and connect replacement to the existing compensation workflow.
- [ ] Run focused tests green.

### Task 5: Documentation and full verification

**Files:**
- Modify: `docs/api/API_CONTRACT.md`
- Modify: `docs/STORAGE.md`
- Modify: `src/Portfolio.Api/appsettings.json`

**Interfaces:**
- Produces: documented Lucide allowlist, grouped response shape, delete conflicts, image URL rules, upload limits, and the `skill-icons` bucket contract.

- [ ] Update section 4 examples and Storage configuration without changing route semantics.
- [ ] Re-check Sprint 3 acceptance criteria and relevant bug lesson `BUG-2026-001` (API factories must disable external services with early settings).
- [ ] Run `dotnet restore`, `dotnet build --configuration Release`, `dotnet test --configuration Release`, and `dotnet format --verify-no-changes`.
- [ ] Inspect `git diff --check` and the final diff; report any Docker-dependent tests that could not run rather than silently skipping them.
