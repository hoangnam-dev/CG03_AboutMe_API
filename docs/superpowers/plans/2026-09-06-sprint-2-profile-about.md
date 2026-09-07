# Sprint 2 Profile and About Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the bilingual public Profile/About slice and authorized administrator editing, including safe avatar and hero-image replacement.

**Architecture:** Keep the existing Controller -> IService -> feature IRepository -> EF Core flow. Public repository methods project only publishable/requested-locale fields with no tracking; services own validation, upsert rules, storage-key generation, and replacement compensation. The existing `avatar_url` and `hero_image_url` columns retain their approved schema but store object keys, as required by `docs/STORAGE.md`; response mapping resolves those keys to public Storage URLs.

**Tech Stack:** .NET 10, ASP.NET Core controller APIs, EF Core 10/Npgsql, Supabase Storage abstraction, xUnit v3.

**Spec:** `docs/superpowers/plans/2026-09-05-personal-portfolio-backend-roadmap.md` Sprint 2, `docs/api/API_CONTRACT.md` section 3, `docs/database.md`, and `docs/security/THREAT_MODEL.md`.

## Global Constraints

- Support exactly `en` and `vi`; omitted locale means `en`, and no locale fallback is allowed.
- Public output omits hidden email/phone, unpublished social links, draft About, storage object keys, and administrator-only fields.
- JSON writes cannot set `avatarUrl` or `heroImageUrl`; those fields mutate only through dedicated multipart endpoints.
- All administrator routes require `AdminPolicy`; identity never comes from request flags.
- No database migration: Sprint 2 uses the Sprint 1 schema and its unique Profile slug, social platform, and one-About-per-Profile constraints.
- Uploads allow non-empty PNG/JPEG/WebP within configured byte limits; object keys are server-generated and replacement follows upload -> persist -> delete-old ordering with compensation on persist failure.
- Cancellation tokens flow from controller through application and persistence/storage operations.

---

### Task 1: Profile and About application contracts and validation

**Files:**
- Create: `src/Portfolio.Application/Profiles/ProfileContracts.cs`
- Create: `src/Portfolio.Application/Profiles/IProfileService.cs`
- Create: `src/Portfolio.Application/Profiles/IProfileRepository.cs`
- Create: `src/Portfolio.Application/About/AboutContracts.cs`
- Create: `src/Portfolio.Application/About/IAboutService.cs`
- Create: `src/Portfolio.Application/About/IAboutRepository.cs`
- Test: `tests/Portfolio.UnitTests/Profiles/ProfileServiceTests.cs`
- Test: `tests/Portfolio.UnitTests/About/AboutServiceTests.cs`

**Interfaces:**
- Produces: `IProfileService.GetPublicAsync(string,string?,CancellationToken)`, `GetAdminAsync`, `UpdateAsync`, `ReplaceAvatarAsync`, and `ReplaceHeroImageAsync`.
- Produces: `IAboutService.GetPublicAsync(string,string?,CancellationToken)`, `GetAdminAsync`, and `UpdateAsync`.
- Produces feature repositories with public DTO projections, tracked update aggregates, conflict checks, and one logical `SaveChangesAsync` call.

- [x] Write service tests whose literal assertions catch hidden contact disclosure, locale mixing, draft About disclosure, duplicate normalized slugs/platforms/orders, incomplete translations, invalid counters/URLs, missing Profile, and read-only media mutation.
- [x] Run the focused tests and confirm compilation/failures are caused by the missing Sprint 2 contracts.
- [x] Add request/response records and small feature-specific interfaces; annotate nullable public fields so policy-hidden properties are absent from JSON.
- [x] Re-run focused tests; contract-only tests must compile while behavior tests remain red.

### Task 2: Profile/About services and media replacement

**Files:**
- Create: `src/Portfolio.Application/Profiles/ProfileService.cs`
- Create: `src/Portfolio.Application/About/AboutService.cs`
- Modify: `src/Portfolio.Application/Common/Storage/IFileStorage.cs`
- Modify: `src/Portfolio.Application/DependencyInjection.cs`
- Modify: `tests/Portfolio.UnitTests/Profiles/ProfileServiceTests.cs`
- Modify: `tests/Portfolio.UnitTests/About/AboutServiceTests.cs`
- Modify: storage fakes implementing `IFileStorage`

**Interfaces:**
- Consumes: Sprint 1 `FileValidation`, `SlugNormalizer`, `PublishTranslationValidation`, `StorageReplacement`, `IFileStorage`, and `TimeProvider`.
- Produces: validated localized public/admin behavior and public media URLs without exposing persisted object keys.

- [x] Implement the minimum validation and mapping needed for one red service case at a time, running each focused test after its implementation.
- [x] Generate media keys as `profiles/{profileId}/{uuid}.{validatedExtension}` in the configured public avatars bucket.
- [x] Extend storage with deterministic public-read URL resolution and use `StorageReplacement.ReplaceAsync` so persistence failure deletes the new object and successful commit precedes old deletion.
- [x] Add structured `LoggerMessage` events containing resource IDs/media kind only, never Profile/About text, filenames, or bytes.
- [x] Run all Profile/About and shared storage unit tests green, then refactor duplication without changing behavior.

### Task 3: EF Core repositories and dependency wiring

**Files:**
- Create: `src/Portfolio.Infrastructure/Persistence/Repositories/ProfileRepository.cs`
- Create: `src/Portfolio.Infrastructure/Persistence/Repositories/AboutRepository.cs`
- Create: `src/Portfolio.Infrastructure/Availability/DatabaseUnavailableProfileRepository.cs`
- Create: `src/Portfolio.Infrastructure/Availability/DatabaseUnavailableAboutRepository.cs`
- Modify: `src/Portfolio.Infrastructure/DependencyInjection.cs`
- Test: `tests/Portfolio.IntegrationTests/Persistence/ProfileRepositoryTests.cs`
- Test: `tests/Portfolio.IntegrationTests/Persistence/AboutRepositoryTests.cs`

**Interfaces:**
- Consumes: the Task 1 repository interfaces and current `PortfolioDbContext` entities/configuration.
- Produces: SQL-side slug/locale/publication filters, requested-locale projections, deterministic social ordering, tracked aggregates for atomic replacement, and single-save upserts.

- [x] Add PostgreSQL integration tests for public projection filtering, requested locale, unique slug, unique social platform, and one About per Profile.
- [x] Run focused integration tests and verify the missing repositories fail the test build/run for the expected reason.
- [x] Implement `AsNoTracking` public/admin reads, tracked mutation reads, duplicate checks, aggregate replacement, and cancellation propagation.
- [x] Register real repositories when PostgreSQL is configured and explicit 503 fallbacks otherwise.
- [x] Run focused persistence tests green; if Docker is unavailable, report rather than silently skipping them.

### Task 4: HTTP controllers, OpenAPI contract, and API security tests

**Files:**
- Create: `src/Portfolio.Api/Controllers/ProfileController.cs`
- Create: `src/Portfolio.Api/Controllers/AboutController.cs`
- Create: `src/Portfolio.Api/Configuration/ProfileMediaOptions.cs`
- Modify: `src/Portfolio.Api/Extensions/ServiceCollectionExtensions.cs`
- Modify: `src/Portfolio.Api/appsettings.json`
- Test: `tests/Portfolio.IntegrationTests/Api/ProfileAboutApiTests.cs`

**Interfaces:**
- Consumes: Task 1 service interfaces and ASP.NET Core `IFormFile` model binding.
- Produces: all section-3 public/admin routes using `ApiResponse<T>` on success and RFC Problem Details on failures.

- [x] Add API tests for anonymous public access, anonymous administrator 401, non-admin 403, missing resources 404, unsupported locale 400, model validation Problem Details, and multipart boundaries.
- [x] Run the focused API tests and confirm they fail because routes are absent.
- [x] Add thin public/admin controller actions, `AdminPolicy`, exact response annotations, multipart conversion, request-size enforcement, and options wiring.
- [x] Run focused API tests green.
- [x] Run `dotnet restore`, `dotnet build --configuration Release`, and `dotnet test --configuration Release`; inspect the diff and re-check all Sprint 2 acceptance criteria before reporting completion.
