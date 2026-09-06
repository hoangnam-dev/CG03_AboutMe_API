# Sprint 1 Shared Foundations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide reusable locale, validation, storage, configuration, migration, and real PostgreSQL test foundations for later portfolio feature sprints.

**Architecture:** Pure cross-feature rules live in `Portfolio.Application`; the Supabase HTTP adapter and its validated options live in `Portfolio.Infrastructure`; EF Core remains the schema source and unit of work. Tests exercise pure behavior in the unit suite, the HTTP wire contract with a fake handler, and actual migrations/constraints with a PostgreSQL Testcontainer.

**Tech Stack:** .NET 10, C#, ASP.NET Core options/typed `HttpClient`, EF Core 10, Npgsql, PostgreSQL 17, Testcontainers.PostgreSql 4.14, xUnit v3.

**Spec:** `docs/superpowers/plans/2026-09-05-personal-portfolio-backend-roadmap.md` (Sprint 1), `docs/adr/0001-mvp-contract-decisions.md`, `docs/testing/TEST_STRATEGY.md`

## Global Constraints

- Preserve the three-project Layered Architecture and do not add CQRS, MediatR, a generic repository, or a custom unit of work.
- Supported locales are exactly `en` and `vi`; omission defaults to `en`; unsupported values are validation errors.
- Durable file metadata stores bucket and object key, never a signed URL.
- Storage credentials remain server-only; storage operations use bounded timeouts and safe errors.
- Preserve the immutable initial migration; schema corrections use one new forward migration.
- New author-managed publishable rows default to Draft without modifying existing rows.
- PostgreSQL integration tests must fail clearly when Docker is unavailable; they must not silently skip.

---

### Task 1: Locale, slug, and publish-translation rules

**Files:**
- Create: `tests/Portfolio.UnitTests/Common/Localization/SupportedLocalesTests.cs`
- Create: `tests/Portfolio.UnitTests/Common/Validation/SlugNormalizerTests.cs`
- Create: `tests/Portfolio.UnitTests/Common/Validation/PublishTranslationValidationTests.cs`
- Create: `src/Portfolio.Application/Common/Localization/SupportedLocales.cs`
- Create: `src/Portfolio.Application/Common/Validation/SlugNormalizer.cs`
- Create: `src/Portfolio.Application/Common/Validation/PublishTranslationValidation.cs`

**Interfaces:**
- Produces: `SupportedLocales.Normalize(string? locale) -> string`, `SlugNormalizer.Normalize(string value) -> string`, and `PublishTranslationValidation.EnsureComplete(IEnumerable<string> locales) -> void`.
- Produces validation failures through the existing `Portfolio.Application.Common.Exceptions.ValidationException` and its typed `Errors` dictionary.

- [ ] **Step 1: Write failing tests** for omitted/allowed/rejected locales, invariant lowercase hyphenated slugs with collapsed separators, and missing `en` or `vi` publish translations.
- [ ] **Step 2: Run tests to verify RED** with `dotnet test tests/Portfolio.UnitTests/Portfolio.UnitTests.csproj --configuration Release --filter "FullyQualifiedName~SupportedLocalesTests|FullyQualifiedName~SlugNormalizerTests|FullyQualifiedName~PublishTranslationValidationTests"` and confirm failures are missing types/methods.
- [ ] **Step 3: Implement minimal pure helpers** using ordinal locale checks, Unicode decomposition for slug diacritics, and a set comparison against `SupportedLocales.All`.
- [ ] **Step 4: Run the filtered tests to verify GREEN.**
- [ ] **Step 5: Commit** with `git commit -m "feat: add shared localization and validation rules"` if the user requests commits.

### Task 2: Signature-based reusable file validation

**Files:**
- Create: `tests/Portfolio.UnitTests/Common/Validation/FileValidationTests.cs`
- Create: `src/Portfolio.Application/Common/Validation/FileValidation.cs`
- Create: `src/Portfolio.Application/Common/Validation/FileValidationOptions.cs`
- Create: `src/Portfolio.Application/Common/Validation/FileUpload.cs`

**Interfaces:**
- Produces: `FileValidation.ValidateAsync(FileUpload upload, FileValidationOptions options, CancellationToken cancellationToken) -> Task`.
- `FileUpload` carries `Stream Content`, untrusted `OriginalFileName`, declared `ContentType`, and `Length`; validation never derives an object key from the filename.
- `FileValidationOptions` defines the maximum length and an explicit extension/MIME/signature allow-list for PNG, JPEG, WebP, and PDF.

- [ ] **Step 1: Write failing tests** proving zero-byte and oversized inputs fail; path-bearing names sanitize to basename metadata; extension/MIME mismatch fails; and valid PNG/JPEG/WebP/PDF signatures pass while preserving stream position.
- [ ] **Step 2: Run the file-validation tests to verify RED.**
- [ ] **Step 3: Implement minimal validation** that checks seek/read capability, length, basename metadata, case-insensitive extension/MIME pairs, magic bytes, and cancellation.
- [ ] **Step 4: Run the file-validation tests to verify GREEN.**
- [ ] **Step 5: Commit** with `git commit -m "feat: add signature-based upload validation"` if requested.

### Task 3: Storage boundary, compensation, and Supabase adapter

**Files:**
- Create: `src/Portfolio.Application/Common/Storage/IFileStorage.cs`
- Create: `src/Portfolio.Application/Common/Storage/StorageUpload.cs`
- Create: `src/Portfolio.Application/Common/Storage/StorageObject.cs`
- Create: `src/Portfolio.Application/Common/Storage/StorageReplacement.cs`
- Create: `src/Portfolio.Infrastructure/Storage/SupabaseStorageOptions.cs`
- Create: `src/Portfolio.Infrastructure/Storage/SupabaseStorageOptionsValidator.cs`
- Create: `src/Portfolio.Infrastructure/Storage/SupabaseFileStorage.cs`
- Modify: `src/Portfolio.Infrastructure/DependencyInjection.cs`
- Create: `tests/Portfolio.UnitTests/Common/Storage/StorageReplacementTests.cs`
- Create: `tests/Portfolio.IntegrationTests/Storage/SupabaseFileStorageContractTests.cs`

**Interfaces:**
- Produces the roadmap's exact `IFileStorage`, `StorageUpload`, and `StorageObject` contracts.
- Produces `StorageReplacement.ReplaceAsync(StorageUpload, string? oldObjectKey, Func<StorageObject,CancellationToken,Task> persist, CancellationToken) -> Task<StorageObject>`; persistence failure triggers best-effort deletion of the new object, while old-object deletion starts only after persistence succeeds.
- Consumes Supabase Storage REST endpoints `/storage/v1/object/{bucket}/{objectKey}` and `/storage/v1/object/sign/{bucket}/{objectKey}` with `apikey` and bearer service-role headers.

- [ ] **Step 1: Write failing compensation tests** recording storage/persistence events and asserting `upload -> persist -> delete-old` on success and `upload -> persist-fails -> delete-new` on failure.
- [ ] **Step 2: Write failing adapter contract tests** for upload headers/path/response, idempotent 404 delete, signed URL resolution, unauthorized/conflict/rate-limit/provider errors, oversized error bodies, and timeout translation without credential leakage.
- [ ] **Step 3: Run both filtered suites to verify RED.**
- [ ] **Step 4: Implement the storage contracts, compensation coordinator, validated options, typed client registration, bounded timeout, response limit, URI escaping, JSON parsing, and safe `ServiceUnavailableException` translation.**
- [ ] **Step 5: Run both filtered suites to verify GREEN.**
- [ ] **Step 6: Commit** with `git commit -m "feat: add Supabase storage foundation"` if requested.

### Task 4: Forward-only Sprint 1 schema correction

**Files:**
- Modify: `src/Portfolio.Application/Common/Models/ProfileModels.cs`
- Modify: `src/Portfolio.Application/Common/Models/CertificateModels.cs`
- Modify: publishable entity configurations in `src/Portfolio.Infrastructure/Persistence/Configurations/*.cs`
- Create: `src/Portfolio.Infrastructure/Persistence/Migrations/<timestamp>_Sprint1SchemaCorrections.cs`
- Create: matching migration designer and update `PortfolioDbContextModelSnapshot.cs` through `dotnet ef migrations add`.
- Modify: `tests/Portfolio.IntegrationTests/Persistence/PersistenceModelTests.cs`

**Interfaces:**
- Produces non-null `Profile.ShowEmail`, `Profile.ShowPhone`, and `Certificate.ShowCredentialId`, each database-defaulted to `false`.
- Changes all approved author-managed `IsPublished` store defaults to `false` without data updates.
- Makes `Certificate.IssuedDate` non-null after a migration SQL precheck raises a clear exception when null rows exist.

- [ ] **Step 1: Add failing metadata tests** for visibility columns, Draft defaults, and non-null certificate issue dates.
- [ ] **Step 2: Run the metadata tests to verify RED.**
- [ ] **Step 3: Update models/configurations and generate one new migration** with a precheck `DO` block before `issued_date` nullability changes; do not edit `InitialCreate`.
- [ ] **Step 4: Run metadata tests to verify GREEN and inspect Up/Down operations manually.**
- [ ] **Step 5: Commit** with `git commit -m "feat: apply Sprint 1 schema corrections"` if requested.

### Task 5: Real PostgreSQL migration and constraint harness

**Files:**
- Create: `tests/Portfolio.IntegrationTests/Infrastructure/PostgreSqlFixture.cs`
- Create: `tests/Portfolio.IntegrationTests/Infrastructure/PostgreSqlCollection.cs`
- Create: `tests/Portfolio.IntegrationTests/Persistence/MigrationTests.cs`
- Create: `tests/Portfolio.IntegrationTests/Persistence/PersistenceConstraintTests.cs`

**Interfaces:**
- Produces one non-parallel collection fixture using pinned `postgres:17-alpine`, calls `Database.MigrateAsync` once, and exposes `CreateDbContext()` plus `ResetApplicationDataAsync()`.
- Reset truncates application tables only, preserves Identity tables and `__EFMigrationsHistory`, and restarts identities/cascades safely inside the disposable test container.

- [ ] **Step 1: Add fixture-backed tests** proving all migrations apply to an empty database, unsupported locales fail with PostgreSQL `CheckViolation`, negative display orders fail, and corrected database defaults are Draft/private.
- [ ] **Step 2: Run the PostgreSQL tests and confirm they fail before the fixture/correction is complete, with a clear Docker prerequisite error if Docker is unavailable.**
- [ ] **Step 3: Implement the collection fixture and targeted cleanup.**
- [ ] **Step 4: Run the PostgreSQL suite to verify GREEN against the real container.**
- [ ] **Step 5: Commit** with `git commit -m "test: add PostgreSQL migration and constraint harness"` if requested.

### Task 6: Configuration and developer documentation

**Files:**
- Modify: `src/Portfolio.Api/appsettings.json`
- Modify: `tests/Portfolio.IntegrationTests/Api/ApiFoundationTests.cs`
- Create or modify: `.env.example`
- Create: `docs/STORAGE.md`
- Modify: `README.md` if present; otherwise create `docs/LOCAL_SETUP.md`.

**Interfaces:**
- Documents `SupabaseStorage__Url`, `SupabaseStorage__ServiceRoleKey`, purpose-specific public/private buckets, `Upload__MaxFileSize`, object-key format, compensation ordering, and Docker/PostgreSQL integration-test prerequisites.
- Development may start without external configuration; non-Development validates storage options at startup.

- [ ] **Step 1: Add failing option/startup tests** for absent/invalid Supabase URL, service key, timeout, response size, and bucket names outside Development.
- [ ] **Step 2: Run the option/startup tests to verify RED.**
- [ ] **Step 3: Bind documented safe defaults/placeholders, preserve Development optional startup, and write setup/storage documentation without secrets.**
- [ ] **Step 4: Run option/startup tests to verify GREEN.**
- [ ] **Step 5: Commit** with `git commit -m "docs: document Sprint 1 storage and test setup"` if requested.

### Task 7: Full verification and plan self-review

**Files:**
- Modify: this plan only to mark completed checkboxes after evidence exists.

- [ ] **Step 1: Run** `dotnet restore`.
- [ ] **Step 2: Run** `dotnet build --configuration Release` and require zero warnings/errors.
- [ ] **Step 3: Run** `dotnet test tests/Portfolio.UnitTests/Portfolio.UnitTests.csproj --configuration Release`.
- [ ] **Step 4: Run** `dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release` with Docker available.
- [ ] **Step 5: Run** `dotnet format --verify-no-changes`.
- [ ] **Step 6: Run** `dotnet ef migrations list --project src/Portfolio.Infrastructure --startup-project src/Portfolio.Api` and inspect the new migration ordering.
- [ ] **Step 7: Re-read the Sprint 1 scope and report exact verification, Docker prerequisites, and any deferred behavior.**

## Self-review

- Spec coverage: all Sprint 1 tasks map to Tasks 1-7; no endpoint work is introduced.
- Placeholder scan: no implementation placeholder is used; concrete interfaces, cases, files, and commands are named.
- Type consistency: the storage boundary matches the roadmap exactly; storage compensation consumes and returns `StorageObject`; locale validation consistently uses string locale codes.

## Execution record

- Implemented Tasks 1-6 inline with failing tests before the corresponding behavior changes.
- Release build: passed with zero warnings and zero errors.
- Unit suite: 37 passed, 0 failed.
- PostgreSQL-backed integration evidence: all 5 migration/constraint tests passed against `postgres:17-alpine`; the pre-hardening full integration run passed 45/45.
- Final integration rerun after signed-URL origin hardening: 47/52 passed; the 5 PostgreSQL fixture cases could not start because the local Docker Desktop engine pipe stopped responding. The affected 12 storage contract tests passed independently after that final change.
- Import/style diagnostic (`IDE0005`): passed.
- Full `dotnet format --verify-no-changes`: not clean because the pre-existing repository uses LF while `.editorconfig` requires CRLF; no unrelated mass line-ending rewrite was made.
- EF migration ordering: `InitialCreate` followed by `Sprint1SchemaCorrections`; the live design-time database was unavailable during `migrations list`, while the same ordering and application were proven by the PostgreSQL Testcontainer suite.
- No commits were created.
