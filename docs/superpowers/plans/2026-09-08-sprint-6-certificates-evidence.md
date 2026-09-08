# Sprint 6 Certificates and Evidence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver bilingual published certificates and administrator CRUD with private, short-lived evidence access and recoverable image/PDF replacement.

**Architecture:** Add a Certificates feature following Controller → `ICertificateService` → `CertificateService` → `ICertificateRepository` → EF Core. The service owns validation, computed expiration/credential disclosure, signed URL generation, and storage compensation; the repository owns projections and aggregate persistence.

**Tech Stack:** .NET 10, ASP.NET Core controllers, EF Core 10, Npgsql/PostgreSQL, Supabase Storage abstraction, xUnit.

**Spec:** `docs/superpowers/plans/2026-09-05-personal-portfolio-backend-roadmap.md` Sprint 6; `docs/api/API_CONTRACT.md` section 7; `docs/BACKEND_SPEC.md` BE-06; `docs/database.md` certificate tables; `docs/security/THREAT_MODEL.md` disclosure and storage controls.

## Global Constraints

- Public reads return only published certificates with exactly the requested `en` or `vi` name.
- Issuer, issue date, localized names, expiration ordering, HTTPS credential URLs, unique/existing technology IDs, and non-negative display order are enforced.
- Publishing requires both `en` and `vi` translations.
- Hidden credential IDs are omitted from public JSON.
- Certificate evidence is PNG/JPEG/WebP/PDF in private `certificate-files` storage; database URL columns contain object keys only.
- Evidence responses use five-minute signed URLs created at read time and never expose object keys.
- New objects are compensated after persistence failure; old objects are deleted only after the metadata commit.
- No database migration is expected because Sprint 1 already established issue-date and credential-visibility constraints.

---

### Task 1: Define and test certificate application behavior

**Files:**
- Create: `tests/Portfolio.UnitTests/Certificates/CertificateServiceTests.cs`
- Create: `src/Portfolio.Application/Certificates/CertificateContracts.cs`
- Create: `src/Portfolio.Application/Certificates/ICertificateService.cs`
- Create: `src/Portfolio.Application/Certificates/ICertificateRepository.cs`
- Create: `src/Portfolio.Application/Certificates/CertificateService.cs`
- Modify: `src/Portfolio.Application/DependencyInjection.cs`

**Interfaces:**
- Consumes: certificate aggregate models, `IFileStorage`, `StorageReplacement`, `TimeProvider`, localization and file validation.
- Produces: public/admin reads, CRUD, and evidence upload operations.

- [ ] **Step 1: Write failing service tests** for invalid dates, incomplete publish translations, unknown/duplicate technologies, credential omission, computed expiration, signed evidence access, and replacement ordering.
- [ ] **Step 2: Run** `dotnet test tests/Portfolio.UnitTests/Portfolio.UnitTests.csproj --configuration Release --filter FullyQualifiedName~CertificateServiceTests` and verify failures are caused by the missing feature.
- [ ] **Step 3: Implement minimal contracts and service** with `CertificateWriteRequest`, public/admin DTOs, a five-minute signed URL response, and generated keys shaped as `certificates/{certificateId}/{uuid}.{extension}`.
- [ ] **Step 4: Re-run the focused tests** and require zero failures.

### Task 2: Implement and test PostgreSQL persistence

**Files:**
- Create: `tests/Portfolio.IntegrationTests/Persistence/CertificateRepositoryTests.cs`
- Create: `src/Portfolio.Infrastructure/Persistence/Repositories/CertificateRepository.cs`
- Create: `src/Portfolio.Infrastructure/Availability/DatabaseUnavailableCertificateRepository.cs`
- Modify: `src/Portfolio.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `ICertificateRepository` and certificate projection/page records.
- Produces: draft-safe exact-locale public reads, bounded admin search, tracked aggregate loading, technology existence checks, add/remove/save.

- [ ] **Step 1: Write failing repository tests** for public filtering/order, exact locale, aggregate persistence, and certificate-technology restrict behavior.
- [ ] **Step 2: Run** the focused repository test filter and verify RED.
- [ ] **Step 3: Implement repository queries** with `AsNoTracking`, deterministic order `(displayOrder, issuedDate desc, id)`, split aggregate loading, and EF-parameterized search.
- [ ] **Step 4: Re-run the focused repository tests** and require zero failures.

### Task 3: Expose and test HTTP routes

**Files:**
- Create: `tests/Portfolio.IntegrationTests/Api/CertificatesApiTests.cs`
- Create: `src/Portfolio.Api/Controllers/CertificatesController.cs`
- Modify: `src/Portfolio.Api/Extensions/ServiceCollectionExtensions.cs`

**Interfaces:**
- Consumes: `ICertificateService`, API envelopes, `AdminPolicy`, ProblemDetails, and `CertificateEvidenceSettings`.
- Produces: all seven certificate routes in API contract section 7, including one bounded multipart `file` upload.

- [ ] **Step 1: Write failing API tests** for OpenAPI routes, anonymous/non-admin rejection, invalid locale, hidden credential serialization, and invalid multipart evidence.
- [ ] **Step 2: Run** the focused API test filter and verify RED.
- [ ] **Step 3: Implement controllers and settings registration** using the existing configured max upload size and `certificate-files` bucket.
- [ ] **Step 4: Re-run the focused API tests** and require zero failures.

### Task 4: Full verification and contract review

**Files:**
- Modify only Sprint 6 files needed to resolve attributable failures.

**Interfaces:**
- Consumes: Tasks 1–3.
- Produces: verified Sprint 6 behavior with unchanged EF schema.

- [ ] **Step 1:** Run `dotnet build Portfolio.sln --configuration Release`.
- [ ] **Step 2:** Run `dotnet test Portfolio.sln --configuration Release --no-build`.
- [ ] **Step 3:** Run `dotnet format Portfolio.sln --verify-no-changes --no-restore`.
- [ ] **Step 4:** Confirm no migration/model snapshot change and re-check Sprint 6 draft, localization, credential privacy, dates, signed URL, and compensation requirements.
