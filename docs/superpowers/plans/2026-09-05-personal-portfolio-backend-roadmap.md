# Personal Portfolio Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the approved CG03 AboutMe personal-portfolio MVP as a secure, bilingual ASP.NET Core REST API, then prepare a separately approved future phase for realtime guest chat without inventing contracts or schema.

**Architecture:** Preserve the repository's three-project Layered Architecture: `Portfolio.Api` owns HTTP and hosting, `Portfolio.Application` owns use-case logic and boundary abstractions, and `Portfolio.Infrastructure` owns EF Core, PostgreSQL, ASP.NET Core Identity, and Supabase Storage adapters. Controllers depend on `IService`, services depend on feature-specific `IRepository` and external-service abstractions, and `PortfolioDbContext` remains the EF Core unit of work.

**Tech Stack:** .NET 10, ASP.NET Core 10 controller-based Web API, C#, EF Core 10, Npgsql, PostgreSQL/Supabase, ASP.NET Core Identity, JWT bearer authentication, Supabase Storage, ProblemDetails, Serilog, Swagger/OpenAPI, xUnit v3, Testcontainers for PostgreSQL, Docker, and GitHub Actions.

**Spec:** `docs/BACKEND_SPEC.md` and `docs/database.md`; repository policy is `AGENTS.md`. The attached planning demand is an input to coverage and risk analysis, but it cannot override those sources of truth.

## Global Constraints

- The current MVP includes Profile/Hero, About, Skills, Work Experience, Projects, Certificates, CV, Contact, and admin CRUD.
- Chat realtime, Redis, AI bot, and group chat are explicitly outside the first release.
- Use the existing three projects only; do not add a `Portfolio.Domain` project.
- Use simple Layered Architecture, not full Clean Architecture.
- Do not introduce CQRS, MediatR, domain events, a generic repository, or a custom unit of work.
- Public content must support exactly `en` and `vi` translations.
- Public APIs must return only published data and must sanitize limited-disclosure project data on the server.
- ASP.NET Core Identity manages the administrator account and roles.
- All `/api/v1/admin/**` endpoints require `AdminPolicy`.
- PostgreSQL is the durable source of truth; file bytes belong in Supabase Storage, never the container filesystem.
- Resume version allocation uses the `Asia/Ho_Chi_Minh` business timezone and is transaction-safe.
- Every controller-to-infrastructure call chain propagates `CancellationToken`.
- Every success response uses `ApiResponse<T>`; every error uses RFC Problem Details with `requestId`.
- Existing migrations are immutable; schema corrections require a new migration.
- All implementation tasks use test-first red-green-refactor and end with focused tests plus `dotnet build --configuration Release`.
- No sprint may add an index without naming the filter, join, uniqueness, or ordering query it supports.

---

## 1. Evidence and Current Baseline

Reviewed on 2026-09-05:

- `AGENTS.md`
- `docs/BACKEND_SPEC.md`
- `docs/database.md`
- `docs/BUG_LESSONS.md` (currently has no recorded bug entries)
- every tracked `.cs` and `.csproj` under `src/` and `tests/`
- `src/Portfolio.Infrastructure/Persistence/Migrations/20260905063820_InitialCreate.cs` and its model snapshot
- root build/package/tool configuration and API appsettings files
- repository inventory for Docker, CI, SQL/RLS, environment templates, and architecture documents

Current implementation status:

| Capability | Status | Evidence |
| --- | --- | --- |
| Three-project solution and dependency direction | Implemented | API references Application and Infrastructure; Infrastructure references Application |
| Global ProblemDetails and request ID | Implemented | `GlobalExceptionHandler`, invalid-model response factory, JWT challenge/forbidden handlers |
| Structured request logging | Implemented through Sprint 2/auth | Serilog request logging plus Profile/About/auth security events without credential/content logging |
| Health checks | Implemented | `/health` and `/health/ready` |
| Admin authentication | Implemented | ASP.NET Core Identity, RS256 access JWT, rotating opaque refresh sessions, CSRF/Origin controls, bootstrap, lockout |
| Admin authorization | Implemented | `AdminPolicy`, required JWT/session claims, immediate active-session/auth-version validation |
| Dashboard | Implemented | Counts projects, certificates, resumes, unread contacts |
| Database schema | Implemented through auth-session migration | Portfolio/Identity tables plus `auth_sessions`, `refresh_tokens`, constraints, relationships, indexes |
| Portfolio feature APIs | Implemented through Sprint 2 | Profile/About public and admin APIs complete; later feature sprints remain pending |
| Supabase Storage | Implemented for Sprint 2 | Storage abstraction/adapter, health check and safe Profile avatar/hero replacement |
| PostgreSQL behavioral integration tests | Implemented through Sprint 2/auth | PostgreSQL Testcontainers cover migrations, constraints, projections and refresh concurrency |
| Rate limiting and spam controls | Partially implemented | Login/refresh IP limits and Identity account lockout exist; Contact spam controls remain pending |
| Docker and CI | Not implemented | No Dockerfile, `.dockerignore`, or `.github/workflows` |
| RLS and Supabase policy artifacts | Not implemented | Initial migration has no RLS policy SQL |

Baseline verification:

```text
dotnet restore                                      PASS
dotnet test --configuration Release --no-build     PASS (156/156)
dotnet build --configuration Release --no-restore  PASS (0 warnings, 0 errors)
```

## 2. Architecture Assessment

The existing structure is coherent for a small portfolio service. Project references enforce the intended dependency direction, controllers are thin, services depend on abstractions, repositories own EF Core, and `PortfolioDbContext` correctly supplies change tracking and transaction APIs. Continue this pattern feature by feature.

Do not move the existing EF-mapped model classes out of `Portfolio.Application/Common/Models` during feature delivery. Their public setters and navigation properties are not ideal domain modeling, but moving them would be a broad refactor with no MVP benefit. Encapsulate business rules in application services and request validators; consider model encapsulation only after feature behavior is covered.

The dashboard repository currently performs four sequential count queries. This is acceptable at MVP volume. Measure it before replacing it with raw SQL or a combined projection.

## 3. Specification Gaps and Required Decisions

Sprint 0 is a hard gate. Record the approved answer for every `GATE-*` item in `docs/adr/0001-mvp-contract-decisions.md` before Sprint 1 begins.

| Gate | Gap or conflict | Recommended decision used by this roadmap |
| --- | --- | --- |
| GATE-01 | The attachment requires Clean Architecture; repository documents explicitly require simple Layered Architecture. | Preserve the existing three-layer architecture. |
| GATE-02 | The attachment names `ARCHITECTURE.md` and `DB_SCHEMA.md`; neither exists. | Keep `AGENTS.md`, `docs/BACKEND_SPEC.md`, and `docs/database.md` authoritative; do not duplicate them under new names. |
| GATE-03 | `BACKEND_SPEC.md` permits Supabase Auth or backend login; `database.md`, source, and migration select ASP.NET Core Identity. | Keep backend-managed ASP.NET Core Identity and admin-only login. Do not add public signup in MVP. |
| GATE-04 | Older owner-scoped wording mentions `user_id`, but `database.md` is a single-portfolio schema with no owner FK on content. | Treat MVP as a single portfolio. `AdminPolicy` authorizes mutations; global project/profile slugs remain correct. Multi-owner support requires a future migration and ADR. |
| GATE-05 | Profile email/phone visibility is required but no visibility columns exist. | Add `show_email` and `show_phone` to `profiles` in a new migration; default both to `false`. |
| GATE-06 | Content defaults to published in the schema, while publish operations require complete EN/VI translations. | Change new-record defaults to draft (`false`) for author-managed content; explicit publish validates required translations. Preserve existing rows during migration. |
| GATE-07 | Certificate issue date is required by BE-06 but nullable in `database.md` and the migration. | Make `issued_date` required after a pre-migration null-data check. |
| GATE-08 | Locale selection/fallback is not defined for most public routes. | Accept `?locale=en|vi`, default to `en`, return 400 for unsupported locales, and do not silently substitute a missing translation on a published resource. |
| GATE-09 | Markdown is mentioned conditionally, but supported fields and sanitization rules are absent. | Store and return plain text in MVP. A future rich-text contract must name fields, syntax, sanitizer, and output encoding. |
| GATE-10 | Profile cache invalidation is required, but caching is neither selected nor configured. | Do not add caching in MVP; remove cache invalidation from acceptance criteria until measurements justify a cache. |
| GATE-11 | BE-09 asks for durable audit logs, while `database.md` omits `audit_logs` and lists full audit logging outside MVP. | Emit structured admin-operation audit events to Serilog; defer a durable audit table to a separate schema decision. |
| GATE-12 | Admin list/detail request and response contracts are incomplete. | Add explicit admin read endpoints documented in Sprint 0 using the same resource nouns, standard `page`, `pageSize`, `search`, and `isPublished` query parameters only where the UI needs them. |
| GATE-13 | Supabase database RLS is required broadly, but local Identity JWTs do not map to `auth.uid()` and the API uses direct Npgsql. | Make the application API the only domain-data access path; deny anonymous Data API access. Use Storage bucket policies and server-only service credentials. Add table RLS only if the selected database role model can be integration-tested without breaking EF migrations. |
| GATE-14 | Storage bucket visibility is not decided. | Use public buckets for `avatars` and `project-images`; use private buckets and short-lived signed URLs for `certificate-files` and `cv-files`. All writes remain server-only. |
| GATE-15 | Delete behavior for referenced technologies/categories is unclear. | Return 409 when a category or technology is still referenced; do not cascade-delete professional history. |
| GATE-16 | Contact notification provider is unspecified. | Persist messages first. Define `IContactNotifier` with a no-op MVP implementation; notification failure is logged and never rolls back the message. |
| GATE-17 | Certificate credential ID visibility is required, but the schema has no visibility column. | Add `certificates.show_credential_id boolean not null default false` in a new migration. |
| GATE-18 | Hero image is part of the MVP/schema, but only avatar upload has a route and arbitrary storage URLs are unsafe. | Add `POST /api/v1/admin/profile/hero-image`; keep media URLs read-only in JSON writes. |

## 4. Conflicts with the Attached Demand

The attachment appears to describe a different or later backend: it names `users`, `timeline_entries`, `conversations`, `conversation_participants`, and `messages`, and requires WebSockets, Redis, public signup, guest linking, and AI providers. None of those tables, modules, contracts, or technologies exist in the approved CG03 MVP; the repository explicitly excludes realtime chat, Redis, AI bot, and group chat.

Therefore:

- Sprints 0-10 below are executable against the current sources of truth.
- Future Sprints F0-F6 cover every deferred capability requested by the attachment, but F0 must first produce an approved Phase 2 specification, database design, threat model, API/event contracts, and ADRs.
- No engineer may create chat tables, events, Redis infrastructure, public signup, or AI provider code before F0 is approved.

## 5. Security Risk Register

| ID | Risk | Severity | Required control |
| --- | --- | --- | --- |
| SEC-01 | Drafts or limited project data leak through public projections | Critical | Dedicated public projections, mandatory `IsPublished` filters, disclosure sanitization tests |
| SEC-02 | Admin login brute force or credential stuffing | High | Identity lockout plus named endpoint rate limit, generic 401 response, no credential logging |
| SEC-03 | Direct Supabase/Data API access bypasses application authorization | Critical | Keep service credentials server-only, restrict exposed schemas/roles, document and test access policy |
| SEC-04 | Upload accepts spoofed MIME, active content, oversized files, or unsafe names | High | Size, extension, signature/MIME allowlist, server-generated object key, no trust in client filename/path |
| SEC-05 | Storage succeeds and DB write fails, leaving an orphan | High | Compensating delete with warning/error event and reconciliation runbook |
| SEC-06 | Old file is deleted before replacement metadata commits | High | Persist new state first; delete old object only after successful commit |
| SEC-07 | Contact endpoint is abused for spam or memory exhaustion | High | Request-size limit, named partitioned rate limit, honeypot, field limits, trusted-proxy rules |
| SEC-08 | Client IP spoofing defeats rate limiting | High | Trust forwarded headers only from configured proxies; use normalized remote IP/hash and bounded partitions |
| SEC-09 | JWT signing key, bootstrap password, storage key, or connection string leaks | Critical | startup validation, secret stores, redacted logs, secret scan in CI, no committed values |
| SEC-10 | Global mutable slugs or IDs are treated as authorization proof | High | Authorize by server identity/policy; never accept `userId` or `isAdmin` permission fields |
| SEC-11 | Signed URL remains usable longer than intended | Medium | Short expiry, private document buckets, deletion for urgent revocation, no signed URL persistence |
| SEC-12 | Unsafe Markdown/HTML causes stored XSS | High | Plain-text MVP contract; encode on clients; no raw HTML acceptance |

Official implementation references: [ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0), [Supabase Storage access control](https://supabase.com/docs/guides/storage/security/access-control), and [Supabase bucket access models](https://supabase.com/docs/guides/storage/buckets/fundamentals). ASP.NET Core supports named/partitioned rate-limiting policies and endpoint application; Supabase private buckets require authenticated access or signed URLs, while service keys bypass Storage RLS and must remain server-only.

## 6. Technical Risk Register

| ID | Risk | Mitigation |
| --- | --- | --- |
| TECH-01 | Existing tests called “integration” do not execute PostgreSQL | Build a reusable Testcontainers fixture and test migration, constraints, transactions, and projections against PostgreSQL. |
| TECH-02 | Resume counter allocation races under concurrent uploads | Execute one PostgreSQL upsert-returning statement inside the metadata transaction and verify with concurrent integration tests. |
| TECH-03 | Network storage cannot share a transaction with PostgreSQL | Use explicit compensation and idempotent delete; never keep a DB transaction open during a slow upload. |
| TECH-04 | Published defaults allow incomplete translations | Migrate defaults to false and centralize `en`/`vi` completeness checks. |
| TECH-05 | Reorder requests contain duplicates, omissions, or foreign IDs | Validate the complete target set and update in one transaction. |
| TECH-06 | EF query includes cause N+1 or over-fetching | Project read-only DTOs in SQL with `AsNoTracking`; inspect generated SQL for project/detail queries. |
| TECH-07 | Initial migration may already be deployed | Never edit it; write forward-only correction migrations with deployment prechecks and rollback notes. |
| TECH-08 | Free-tier cold starts and transient network failures | Keep startup light, provide liveness/readiness, use bounded timeouts, and document retry boundaries. |
| TECH-09 | Current app can start in Development without PostgreSQL | Preserve this developer convenience, but require full configuration and fail-fast validation outside Development. |

## 7. Module Dependency Graph

```text
Approved decisions + baseline verification (Sprint 0)
                    |
                    v
Shared locale/validation/storage/test infrastructure (Sprint 1)
          |                 |                    |
          v                 v                    v
Profile + About (2)   Skills (3)          PostgreSQL test harness
          |                 |
          |                 v
          |           Experiences (4)
          |                 |
          +--------+--------+
                   v
             Projects (5)
                   |
                   +----------------+
                   v                v
            Certificates (6)   Resume/CV (7)
                   |                |
                   +--------+-------+
                            v
                    Contact/Admin (8)
                            |
                            v
                 Security/policy hardening (9)
                            |
                            v
                   Docker/CI/release (10)

Future, separately approved:
Phase 2 spec (F0) -> signup/guest identity (F1) -> conversations (F2)
-> persistent messaging (F3) -> realtime gateway (F4)
-> presence/Redis scale-out (F5) -> AI bot (F6)
```

## 8. Planned File Map and Stable Interfaces

Follow the existing feature folders. Split request, response, validator, service, and repository contracts into focused files rather than growing `Common/Models`.

```text
src/Portfolio.Api/
  Controllers/{Profile,About,Skills,Experiences,Projects,Certificates,Resumes,Contacts}Controller.cs
  Configuration/{Storage,Upload,RateLimit}Options*.cs
  Extensions/ServiceCollectionExtensions.cs
  Program.cs

src/Portfolio.Application/
  Common/Localization/SupportedLocales.cs
  Common/Storage/{IFileStorage,StorageObject,StorageUpload}.cs
  Common/Validation/{FileValidation,PublishTranslationValidation,SlugNormalizer}.cs
  Common/Notifications/IContactNotifier.cs
  {Profiles,About,Skills,Experiences,Projects,Certificates,Resumes,Contacts}/
    I{Feature}Service.cs
    {Feature}Service.cs
    I{Feature}Repository.cs
    Dtos/*.cs
    Validators/*.cs

src/Portfolio.Infrastructure/
  Persistence/Repositories/{Feature}Repository.cs
  Storage/SupabaseFileStorage.cs
  Storage/SupabaseStorageOptions*.cs
  Notifications/LoggingContactNotifier.cs
  Persistence/Migrations/<forward-only migrations>.cs

tests/Portfolio.UnitTests/<Feature>/*Tests.cs
tests/Portfolio.IntegrationTests/
  Infrastructure/PostgreSqlFixture.cs
  Persistence/<Feature>RepositoryTests.cs
  Api/<Feature>ApiTests.cs
  Storage/SupabaseFileStorageContractTests.cs
```

Stable storage boundary to approve in Sprint 1:

```csharp
public interface IFileStorage
{
    Task<StorageObject> UploadAsync(StorageUpload upload, CancellationToken cancellationToken);
    Task DeleteIfExistsAsync(string bucket, string objectKey, CancellationToken cancellationToken);
    Task<Uri> CreateSignedReadUrlAsync(
        string bucket,
        string objectKey,
        TimeSpan lifetime,
        CancellationToken cancellationToken);
}

public sealed record StorageUpload(
    string Bucket,
    string ObjectKey,
    Stream Content,
    string ContentType,
    long Length);

public sealed record StorageObject(string Bucket, string ObjectKey);
```

Stable repository rule: feature repositories expose meaningful queries and one logical persistence operation; they do not expose `IQueryable`, `DbSet`, generic CRUD, or authorization decisions.

---

## 9. Full Sprint Roadmap

| Sprint | Usable increment | Depends on |
| --- | --- | --- |
| 0 | Approved architecture/API/security decisions and verified baseline | None |
| 1 | Shared locale, validation, storage, and real PostgreSQL testing foundations | 0 |
| 2 | Public/admin Profile and About | 1 |
| 3 | Public/admin bilingual Skills | 1 |
| 4 | Public/admin Work Experience | 3 |
| 5 | Public/admin Projects with disclosure controls and gallery | 3, 4 |
| 6 | Public/admin Certificates with evidence uploads | 1, 3 |
| 7 | Current CV download and transaction-safe version management | 1 |
| 8 | Public Contact intake and admin inbox/dashboard integration | 1 |
| 9 | Cross-feature authorization, RLS/access-policy, audit-event, and abuse hardening | 2-8 |
| 10 | Docker, CI, operational docs, migration rehearsal, and release verification | 9 |

## Sprint 0 — Specification and Architecture Gate

### Goal

Produce an approved, internally consistent contract that resolves all `GATE-*` items and freezes the executable MVP scope.

### Dependencies

None.

### Scope

Architecture, API, security, testing, and deployment decisions only; no production code.

### Tasks

- [x] Create `docs/adr/0001-mvp-contract-decisions.md` with the accepted decision, considered options, and consequences for GATE-01 through GATE-18.
- [x] Create `docs/api/API_CONTRACT.md` containing every approved route, authorization rule, query parameter, request shape, response shape, status code, pagination rule, and ProblemDetails behavior.
- [x] Create `docs/security/THREAT_MODEL.md` covering public reads, admin login/mutations, database access, Storage, contact abuse, and secret handling.
- [x] Create `docs/testing/TEST_STRATEGY.md` defining unit, PostgreSQL integration, API, Storage contract, security, and regression suites.
- [x] Record the current build/test baseline and the fact that `docs/BUG_LESSONS.md` has no applicable entries in `docs/testing/BASELINE.md`.
- [x] Review the approved documents in `docs/reviews/SPRINT_0_ARCHITECTURE_GATE.md` before any production code or migration is written.

### API / Events

No runtime endpoint or event changes. This sprint freezes contracts only.

### Database Impact

No database mutation. Document the planned forward-only corrections for profile visibility, draft defaults, and certificate issue-date nullability.

### Security Review

Approve the trust boundaries for browser, API, direct PostgreSQL, Supabase Data API, Storage API, and deploy platform. Explicitly state that client identifiers and flags never prove authorization.

### Edge Cases

- Existing production data may contain null certificate issue dates.
- Existing published records may lack both translations.
- The initial migration may or may not have been applied to Supabase.
- A frontend may already depend on an undocumented response shape.

### Unit Tests

No production unit tests. Add a lightweight contract-document checklist in the PR description and link every BE-01 through BE-09 requirement to a later sprint.

### Integration Tests

No runtime integration tests. Verification is document traceability plus the unchanged baseline build/test suite.

### Documentation

Create the ADR, API contract, threat model, and test strategy named above.

### Definition of Done

- All 18 gate decisions are approved.
- Every MVP route has an exact contract or is explicitly excluded.
- The attachment-only features are recorded as Phase 2, not MVP.
- No production code changed.

## Sprint 1 — Shared Foundations and PostgreSQL Test Harness

### Goal

Provide reusable locale, validation, file-storage, configuration, and real PostgreSQL integration-test infrastructure needed by all feature sprints.

### Dependencies

Sprint 0.

### Scope

Cross-feature locale, validation, file-storage, configuration, migration, and PostgreSQL test infrastructure.

### Tasks

- [x] Add failing tests for `SupportedLocales.Normalize` accepting `en`/`vi`, defaulting an omitted locale to `en`, and rejecting every other value.
- [x] Add `SupportedLocales`, slug normalization, publish-translation validation, and typed validation errors in `Portfolio.Application/Common`.
- [x] Add failing tests for zero-byte, oversized, extension/MIME mismatch, unsafe filename metadata, and allowed image/PDF signatures.
- [x] Implement reusable file validation without trusting `IFormFile.FileName` or caller-provided storage paths.
- [x] Define `IFileStorage`, `StorageUpload`, and `StorageObject`; add validated `SupabaseStorageOptions` and per-purpose bucket options.
- [x] Implement `SupabaseFileStorage` as a typed `HttpClient` adapter with bounded timeout and safe error translation; never manipulate the Supabase `storage` schema directly.
- [x] Add upload-compensation tests proving a newly uploaded object is deleted after persistence failure and an old object remains until the new state commits.
- [x] Add `PostgreSqlFixture` using `Testcontainers.PostgreSql`, apply migrations once per test collection, reset portfolio data between tests, and preserve Identity schema.
- [x] Convert model-only persistence checks into PostgreSQL constraint tests while retaining fast metadata assertions where valuable.
- [x] Register new abstractions centrally in Application/Infrastructure DI and validate all configuration at startup outside Development.

### API / Events

No user-facing feature endpoint. Existing `/health` and `/health/ready` remain unchanged.

### Database Impact

Create one forward-only migration containing only approved Sprint 0 corrections:

- `profiles.show_email boolean not null default false`
- `profiles.show_phone boolean not null default false`
- `certificates.show_credential_id boolean not null default false`
- draft defaults for approved author-managed `is_published` columns
- `certificates.issued_date` non-null only after a data precheck/backfill decision

Do not add RLS or feature indexes in this migration. Rollback restores old defaults/nullability and removes only newly added visibility columns after confirming consumers no longer use them.

### Security Review

- Storage service credential is server-only and redacted from logs.
- Public/private bucket choice matches GATE-14.
- Object keys are generated from resource IDs and random UUIDs.
- Timeouts and response-body limits prevent hung or oversized provider responses.

### Edge Cases

- Cancellation during upload.
- Provider returns success without a usable object identifier.
- Compensating delete also fails.
- Testcontainer startup is unavailable on a developer machine; unit tests must still run, while integration tests fail with a clear prerequisite message rather than silently skip in CI.

### Unit Tests

```text
SupportedLocalesTests.NormalizeReturnsEnForMissingLocale
SupportedLocalesTests.NormalizeRejectsUnsupportedLocale
SlugNormalizerTests.NormalizeProducesLowercaseHyphenatedSlug
PublishTranslationValidationTests.RequiresEnglishAndVietnamese
FileValidationTests.RejectsZeroByteFile
FileValidationTests.RejectsExtensionMimeMismatch
FileValidationTests.RejectsFileOverConfiguredLimit
StorageCompensationTests.DeletesNewObjectWhenPersistenceFails
StorageCompensationTests.KeepsOldObjectUntilNewMetadataCommits
```

### Integration Tests

```text
MigrationTests.InitialAndCorrectionMigrationsApplyToEmptyPostgreSql
PersistenceConstraintTests.RejectsUnsupportedLocale
PersistenceConstraintTests.RejectsNegativeDisplayOrder
SupabaseFileStorageContractTests.TranslatesUnauthorizedAndTimeoutResponses
```

### Documentation

Update local setup, environment variables, bucket visibility, upload lifecycle, and PostgreSQL test prerequisites.

### Definition of Done

- All shared tests pass.
- A real PostgreSQL container proves migrations and key constraints.
- No service can persist a signed URL as durable metadata.
- `dotnet build --configuration Release` and all tests pass.

## Sprint 2 — Profile and About

### Goal

Deliver the first complete bilingual public portfolio slice and secure admin editing for Profile, social links, About, and avatar.

### Dependencies

Sprint 1.

### Scope

Profile, social links, About, avatar storage, and their public/admin HTTP contracts.

### Tasks

- [x] Create failing service tests for public published projection, locale selection, email/phone visibility, draft About exclusion, admin updates, and missing profile.
- [x] Define `IProfileRepository` queries by slug and the single admin profile, using DTO projections and `AsNoTracking` for reads.
- [x] Implement `ProfileService` and `AboutService`; validate names, title, short bio, counters, URLs, translation completeness on publish, and server-side visibility flags.
- [x] Implement thin public/admin controllers and exact OpenAPI annotations.
- [x] Implement avatar upload with the Sprint 1 storage boundary and compensation sequence.
- [x] Implement Hero image upload with the same validation and compensation sequence.
- [x] Add structured events for profile update, About update, and avatar replacement without logging content or file bytes.

### API / Events

```text
GET  /api/v1/portfolio/{slug}/profile?locale=en
PUT  /api/v1/admin/profile
POST /api/v1/admin/profile/avatar
POST /api/v1/admin/profile/hero-image
GET  /api/v1/portfolio/{slug}/about?locale=en
PUT  /api/v1/admin/about
```

No WebSocket events.

### Database Impact

Uses profile visibility columns approved in Sprint 1. No additional schema change. Existing unique slug and profile/About one-to-one constraints support the queries.

### Security Review

Public DTO never contains hidden email/phone, draft About, internal storage keys, or admin-only fields. Admin endpoints rely on `AdminPolicy`, not request flags.

### Edge Cases

- Unknown slug.
- Requested locale translation absent.
- Duplicate normalized slug.
- Social-link platform duplication.
- Avatar upload succeeds but profile save fails.
- Replacement cleanup fails after the new avatar becomes active.

### Unit Tests

```text
ProfileServiceTests.GetPublicHidesPrivateContactFields
ProfileServiceTests.GetPublicReturnsRequestedTranslationOnly
ProfileServiceTests.UpdateRejectsDuplicateSlug
ProfileServiceTests.UpdateRejectsIncompletePublishedTranslations
ProfileServiceTests.ReplaceAvatarCompensatesOnPersistenceFailure
AboutServiceTests.GetPublicDoesNotReturnDraft
AboutServiceTests.UpdateRejectsNegativeCounters
```

### Integration Tests

Test public projection filtering, unique profile slug, one About per profile, anonymous/admin API status codes, ProblemDetails, and avatar adapter failure translation.

### Documentation

Update API contract/OpenAPI, bucket/object-key conventions, and example bilingual payloads.

### Definition of Done

Both public endpoints return only approved localized data; all admin mutations are authorized, validated, logged, and tested; avatar compensation is verified.

## Sprint 3 — Skills and Categories

### Goal

Deliver ordered bilingual skill groups with secure admin CRUD and the documented icon contract.

### Dependencies

Sprint 1.

### Scope

Skill categories, technologies, bilingual names, icon validation/upload, publishing, and ordering.

### Tasks

- [x] Write failing tests for category/technology CRUD, duplicate translated names, category membership, icon types, publish completeness, restricted deletes, and reorder atomicity.
- [x] Define `ISkillRepository` around public grouped projection, admin reads, duplicate checks, reference checks, writes, and transactional reorder.
- [x] Implement validators for `Primary|Experienced|Familiar|Learning` and `{ type, value }` icon rules, including the Lucide allowlist shared with the frontend.
- [x] Implement public/admin controllers and optional icon-upload endpoint only if approved in Sprint 0.
- [x] Ensure public SQL filters both category and technology publication state and orders in the database.

### API / Events

```text
GET    /api/v1/portfolio/{slug}/skills?locale=en
POST   /api/v1/admin/skill-categories
PUT    /api/v1/admin/skill-categories/{id}
DELETE /api/v1/admin/skill-categories/{id}
POST   /api/v1/admin/skills
PUT    /api/v1/admin/skills/{id}
DELETE /api/v1/admin/skills/{id}
PATCH  /api/v1/admin/skills/reorder
POST   /api/v1/admin/skills/{id}/icon   (only when GATE contract approves upload)
```

### Database Impact

No schema change expected. Existing unique translation name, icon check constraint, category FK, partial public indexes, and display-order checks are used. Do not add an index unless generated SQL exposes an unsupported admin query.

### Security Review

Reject raw SVG/HTML content, arbitrary storage keys, non-HTTPS external image URLs, disallowed hosts, and category IDs that do not exist. Return 409 for referenced-resource deletes.

### Edge Cases

Duplicate IDs/orders in reorder, partial target list, mixed draft/published state, Unicode names, image icon replacement failure, and concurrent reorder requests.

### Unit Tests

```text
SkillServiceTests.CreateRejectsDuplicateNameWithinLocale
SkillServiceTests.PublishRequiresEnglishAndVietnameseNames
SkillServiceTests.DeleteCategoryWithTechnologiesReturnsConflict
SkillServiceTests.ReorderRejectsDuplicateIds
SkillIconValidatorTests.RejectsTextLongerThanSixCharacters
SkillIconValidatorTests.RejectsLucideNameOutsideAllowlist
SkillIconValidatorTests.RejectsUnmanagedImageUrl
```

### Integration Tests

Verify check constraints, FK restrict behavior, public grouping/filter/order SQL, reorder rollback, and admin authorization.

### Documentation

Document the icon allowlist, upload contract, public grouping response, and delete-conflict semantics.

### Definition of Done

Published bilingual skill groups are queryable without N+1 behavior; CRUD, icon validation, restricted deletes, and atomic reorder are proven by tests.

## Sprint 4 — Work Experience

### Goal

Deliver bilingual work history with highlights, technology links, deterministic ordering, and admin CRUD/reorder.

### Dependencies

Sprint 3 for technology references.

### Scope

Work experiences, translations, highlights, technology links, publication, and ordering.

### Tasks

- [x] Write failing tests for dates, `isCurrent` derivation, translations, highlights, referenced technologies, publish, delete, and reorder.
- [x] Define `IExperienceRepository` with public projection and atomic aggregate persistence.
- [x] Implement service validation for required company/start date, localized position, end-date rules, technology existence, and highlight ordering.
- [x] Implement controllers, OpenAPI, and structured admin-operation logs.

### API / Events

```text
GET    /api/v1/portfolio/{slug}/experiences?locale=en
POST   /api/v1/admin/experiences
PUT    /api/v1/admin/experiences/{id}
DELETE /api/v1/admin/experiences/{id}
PATCH  /api/v1/admin/experiences/reorder
```

### Database Impact

No schema change expected. Use existing date constraint, translation/highlight FKs, unique highlight order, technology restrict FK, and public order index.

### Security Review

Public responses exclude drafts and only project the requested locale. URLs are validated as HTTP(S); descriptions remain plain text.

### Edge Cases

Current experience with an end date, non-current experience without an end date if the approved contract forbids it, duplicate highlights, deleted technology, concurrent reorder, and empty published translation.

### Unit Tests

```text
ExperienceServiceTests.CreateRejectsEndBeforeStart
ExperienceServiceTests.CurrentExperienceRequiresNullEndDate
ExperienceServiceTests.PublishRequiresBothTranslations
ExperienceServiceTests.CreateRejectsUnknownTechnology
ExperienceServiceTests.ReorderIsAllOrNothing
```

### Integration Tests

Verify PostgreSQL date/check/FK constraints, aggregate save transaction, public filter/order projection, and API 401/403/404/409 responses.

### Documentation

Document `isCurrent` as derived from `end_date IS NULL`, highlight types, technology linking, and ordering.

### Definition of Done

Work history is complete, ordered, bilingual, draft-safe, transactionally persisted, and covered at service/repository/API boundaries.

## Sprint 5 — Projects and Gallery

### Goal

Deliver public project lists/details and secure admin project/gallery management with enforced disclosure levels.

### Dependencies

Sprints 3 and 4.

### Scope

Project list/detail, translations, technology links, highlights, images, disclosure, publication, and ordering.

### Tasks

- [ ] Write disclosure-first tests before repository or controller code.
- [ ] Define separate `PublicProjectListItem`, `PublicProjectDetail`, and `AdminProject` DTOs so limited data cannot leak through reuse.
- [ ] Define `IProjectRepository` for normalized-slug conflict checks, public list/detail projections, aggregate CRUD, publish, image persistence, and reorder.
- [ ] Implement publish validation requiring both translations and valid related technology/image alt text.
- [ ] Implement gallery upload/replacement using server-generated keys and compensation.
- [ ] Inspect generated SQL for list and detail queries and confirm existing indexes match filters/order.

### API / Events

```text
GET    /api/v1/portfolio/{slug}/projects?locale=en
GET    /api/v1/portfolio/{slug}/projects/{projectSlug}?locale=en
POST   /api/v1/admin/projects
PUT    /api/v1/admin/projects/{id}
DELETE /api/v1/admin/projects/{id}
POST   /api/v1/admin/projects/{id}/images
PATCH  /api/v1/admin/projects/{id}/publish
PATCH  /api/v1/admin/projects/reorder
```

### Database Impact

No schema change expected after Sprint 1 draft defaults. Use global unique project slug because MVP is single-portfolio. Existing partial public index supports kind/disclosure/featured/order; add no speculative search index.

### Security Review

Limited projects must omit `clientContext`, repository URL, sensitive result/problem/solution text, and any other fields named by the Sprint 0 disclosure matrix. Enforcement belongs in server-side projection, not post-serialization mutation.

### Edge Cases

Slug normalization collisions, draft detail lookup, missing locale, limited disclosure, mixed successful/failed multi-file upload, duplicate file indexes, missing alt text, stale image replacement, and broadcast/cache assumptions (there is no cache or realtime in MVP).

### Unit Tests

```text
ProjectServiceTests.GetPublicListExcludesDrafts
ProjectServiceTests.GetLimitedDetailRemovesSensitiveFields
ProjectServiceTests.CreateRejectsDuplicateNormalizedSlug
ProjectServiceTests.PublishRequiresCompleteTranslationsAndAltText
ProjectServiceTests.UploadGalleryCompensatesEveryNewObjectOnFailure
ProjectServiceTests.ReorderRejectsForeignOrDuplicateIds
```

### Integration Tests

Verify public list/detail SQL, global slug uniqueness, date constraints, technology FKs, image/translation cascade, transaction rollback, authorization, multipart limits, and ProblemDetails.

### Documentation

Document the disclosure matrix, multipart `fileIndex` mapping, gallery lifecycle, and exact public/admin DTO differences.

### Definition of Done

No draft or limited-disclosure secret can be returned by public APIs; gallery failures do not corrupt DB state or delete the prior file; list/detail queries are bounded and indexed.

## Sprint 6 — Certificates and Evidence Files

### Goal

Deliver published bilingual certificates and admin CRUD with safe image/PDF evidence management.

### Dependencies

Sprints 1 and 3.

### Scope

Certificates, translations, technology links, credential privacy, and evidence storage.

### Tasks

- [ ] Write failing tests for required issuer/name/issue date, expiration ordering, translation completeness, technology links, credential visibility, and upload compensation.
- [ ] Define certificate service/repository/DTO boundaries and public/admin projections.
- [ ] Implement private evidence storage with short-lived signed URL generation at read time.
- [ ] Implement controllers, validation, logs, and tests.

### API / Events

```text
GET    /api/v1/portfolio/{slug}/certificates?locale=en
POST   /api/v1/admin/certificates
PUT    /api/v1/admin/certificates/{id}
DELETE /api/v1/admin/certificates/{id}
POST   /api/v1/admin/certificates/{id}/file
```

### Database Impact

Uses Sprint 1 non-null `issued_date` correction. No further schema change. `file_url` stores the private object key, never the generated signed URL.

### Security Review

Credential ID visibility follows the approved contract. Only configured image/PDF types are accepted. Private evidence URLs are short-lived and never logged.

### Edge Cases

Non-expiring certificates, expiration equal to issue date, signed URL provider failure, deleted object with retained metadata, and replacement cleanup failure.

### Unit Tests

```text
CertificateServiceTests.CreateRequiresIssueDate
CertificateServiceTests.CreateRejectsExpirationBeforeIssueDate
CertificateServiceTests.GetPublicHidesCredentialIdWhenConfiguredPrivate
CertificateServiceTests.PublishRequiresBothTranslations
CertificateServiceTests.ReplaceEvidencePreservesOldObjectUntilCommit
```

### Integration Tests

Verify date constraint/nullability, technology FK restrict, public filtering/order, private-file response contract, and admin authorization.

### Documentation

Document accepted evidence types/limits, signed URL lifetime, computed expired state, and credential privacy.

### Definition of Done

Certificates are bilingual and draft-safe; evidence storage is private and recoverable across partial failures; tests cover all date and privacy rules.

## Sprint 7 — Resume Versioning and Current CV

### Goal

Deliver current CV access and concurrency-safe EN/VI resume version administration without overwriting history.

### Dependencies

Sprint 1.

### Scope

Resume upload, version counters, active transitions, private Storage, signed access, listing, and deletion.

### Tasks

- [ ] Write service tests for PDF validation, server-generated versions, active transitions, current-delete conflict, replacement semantics, and compensation.
- [ ] Define `IResumeRepository` with a single transaction-owning operation for counter allocation plus Resume insert/active switch.
- [ ] Implement counter allocation using PostgreSQL `INSERT ... ON CONFLICT ... DO UPDATE ... RETURNING` in the same transaction as metadata writes.
- [ ] Derive display version as `v{year}_{sequence:00}` and never accept it from the client.
- [ ] Implement private CV storage and signed-download URL creation.
- [ ] Add a concurrent PostgreSQL test that launches multiple allocations for the same language/year and proves unique monotonic sequences.

### API / Events

```text
GET    /api/v1/portfolio/{slug}/cv/current?language=en
GET    /api/v1/admin/cv
POST   /api/v1/admin/cv
PATCH  /api/v1/admin/cv/{id}/current
DELETE /api/v1/admin/cv/{id}
```

### Database Impact

No schema change expected. Use the counter PK, Resume alternate key, counter FK, and partial unique active-per-language index. Transaction rollback must include both sequence increment and Resume changes.

### Security Review

Accept PDF only for public CV, enforce strict size/signature validation, keep buckets private, use short-lived signed URLs, and never return storage credentials or raw object keys.

### Edge Cases

Year boundary in `Asia/Ho_Chi_Minh`, concurrent first upload, delete non-current/current, inactive published record, DB failure after upload, signed URL failure after metadata read, cancellation during active switch, and sequence gaps after a committed upload later deleted (gaps are allowed).

### Unit Tests

```text
ResumeServiceTests.RejectsClientSuppliedVersionField
ResumeServiceTests.UploadRejectsNonPdfAndZeroByteFile
ResumeServiceTests.UploadFailureKeepsOldCurrentResume
ResumeServiceTests.SetCurrentDoesNotCreateNewVersion
ResumeServiceTests.DeleteCurrentWithoutReplacementReturnsConflict
ResumeVersionFormatterTests.FormatsSequenceWithAtLeastTwoDigits
```

### Integration Tests

```text
ResumeRepositoryTests.ConcurrentUploadsAllocateUniqueSequences
ResumeRepositoryTests.UploadRollsBackCounterAndMetadataTogether
ResumeRepositoryTests.SetCurrentPreservesOneActivePerLanguage
ResumeRepositoryTests.EnglishAndVietnameseCountersAreIndependent
ResumeApiTests.PublicCurrentReturnsShortLivedDownloadUrl
```

### Documentation

Document version algorithm, timezone, transaction boundary, storage compensation, signed URL behavior, and deletion rules.

### Definition of Done

Concurrency tests prove unique versions and one active Resume per language; file replacement never overwrites history; public download exposes only a short-lived URL or approved stream.

## Sprint 8 — Contact Intake and Admin Inbox

### Goal

Deliver abuse-resistant public contact submission plus a private paginated admin inbox and updated dashboard counts.

### Dependencies

Sprint 1.

### Scope

Public contact submission, abuse controls, notification side effect, admin inbox, and dashboard count integration.

### Tasks

- [x] Write validation, honeypot, rate-limit, persistence, notification-failure, status-transition, pagination, and authorization tests first.
- [x] Define `IContactRepository`, `IContactService`, `IContactNotifier`, request/response DTOs, and named `ContactSubmission` rate-limit policy.
- [x] Partition limits by trusted normalized remote IP or a privacy-preserving hash; never use arbitrary client headers unless forwarded-header trust is configured.
- [x] Persist before invoking `IContactNotifier`; log notification failure and return successful creation once persistence commits.
- [x] Implement admin list/detail/status/delete and ensure public users can never enumerate messages.
- [x] Keep dashboard unread count consistent with `ContactStatus.New`.

### API / Events

```text
POST   /api/v1/contact
GET    /api/v1/admin/contacts?page=1&pageSize=20&status=new&search=
GET    /api/v1/admin/contacts/{id}
PATCH  /api/v1/admin/contacts/{id}/status
DELETE /api/v1/admin/contacts/{id}
GET    /api/v1/admin/dashboard   (existing, behavior verified)
```

### Database Impact

No schema change expected. Existing `(status, created_at desc)` index supports the primary inbox filter/order. Do not add a text-search index until search semantics and volume justify one.

### Security Review

Apply request-body and field limits, email validation, generic responses, honeypot handling, no body logging, privacy-preserving IP storage, configured forwarded headers, and 429 ProblemDetails with `Retry-After` when available.

### Edge Cases

Unknown proxy, missing remote IP, duplicate submissions, notifier timeout, status/read timestamp transitions, deleting an unread message, search over large text, and cancellation after DB commit.

### Unit Tests

```text
ContactServiceTests.CreateRejectsInvalidEmailAndFieldLengths
ContactServiceTests.CreateTreatsFilledHoneypotAsSpamWithoutInformationLeak
ContactServiceTests.NotificationFailureDoesNotLosePersistedMessage
ContactServiceTests.MarkReadSetsReadAt
ContactServiceTests.ArchiveDoesNotRewriteReadAt
ContactServiceTests.PublicContractNeverReturnsInboxData
```

### Integration Tests

Verify named rate limiter and 429 response, anonymous POST, anonymous GET rejection/nonexistence, admin pagination/filter/order, status constraint, dashboard count, and notification failure response.

### Documentation

Document limits, proxy trust, rate-limit values/configuration, honeypot field, retention expectations, notification behavior, and privacy treatment.

### Definition of Done

Public contact creation is validated and rate-limited; inbox reads are admin-only; notification outages cannot lose committed messages; dashboard counts remain correct.

## Sprint 9 — Cross-Feature Security and Policy Hardening

### Goal

Prove that the complete MVP enforces authentication, authorization, publication, disclosure, Storage, database-access, CORS, and observability policies consistently.

### Dependencies

Sprints 2-8.

### Scope

Cross-feature security tests, login throttling, database/Storage access policies, audit events, CORS, and production configuration validation.

### Tasks

- [ ] Add a table-driven API security suite covering anonymous, authenticated non-admin, and admin access for every `/admin` endpoint.
- [ ] Add cross-feature public leak tests for drafts, missing translations, hidden contacts, limited projects, private credentials, and storage keys.
- [ ] Add named rate limiting to login while preserving Identity lockout; verify generic failure responses.
- [ ] Create `docs/supabase/database-access.sql` or a Supabase dashboard runbook implementing the approved GATE-13 role/RLS decision; do not mutate the managed `storage` schema directly.
- [ ] Create Storage bucket/policy scripts or dashboard steps for server-only writes and approved public/private reads.
- [ ] Add structured, source-generated log events for create/update/delete/publish/file actions and verify secrets/content are absent.
- [ ] Add configuration-validation tests for production startup, CORS origin, JWT, DB, Storage, upload limits, rate limits, and bootstrap settings.
- [ ] Run `dotnet format --verify-no-changes` and resolve only issues caused by feature work.

### API / Events

No new feature endpoints. Login and existing endpoints gain documented throttling/security behavior.

### Database Impact

Only approved access-role/RLS policy scripts. Domain schema changes are not expected. Provide an explicit rollback script for every policy/grant change and test with the same role used by production EF Core.

### Security Review

Close SEC-01 through SEC-12 with executable evidence or a documented accepted risk and owner. Verify service-role keys never reach clients or logs.

### Edge Cases

Expired/tampered/wrong-audience tokens, deleted/disabled admin, CORS from unapproved origin, proxy misconfiguration, unavailable Storage/DB, and policy deployment before application deployment.

### Unit Tests

Configuration validators, disclosure policies, audit-event redaction, and authorization-decision pure logic.

### Integration Tests

Full authorization matrix, JWT failures, login/contact throttling, production startup validation, Supabase role/policy smoke tests where available, and Storage access matrix.

### Documentation

Finalize threat model, security checklist, secret rotation, access policy, incident diagnostics, and accepted risks.

### Definition of Done

Every security risk has evidence; all admin routes reject anonymous/non-admin callers correctly; public leak tests pass; production configuration fails fast; access-policy rollback is documented.

## Sprint 10 — Container, CI, Release, and Operations

### Goal

Produce a reproducible, non-root container and a CI/release process that restores, builds, tests, scans, migrates safely, and verifies production health.

### Dependencies

Sprint 9.

### Scope

Container build/runtime, CI, environment reference, migration rehearsal, deployment, rollback, and production smoke verification.

### Tasks

- [ ] Add a multi-stage `Dockerfile` and `.dockerignore`; use the .NET 10 runtime image, port 8080, and a non-root user supported by the image.
- [ ] Add `.github/workflows/backend-ci.yml` with pinned major official actions, least permissions, restore, Release build, test, format verification, and Docker build.
- [ ] Add `.env.example` containing names and safe placeholders only, aligned with actual options (`ConnectionStrings`, `Frontend`, `Jwt`, `BootstrapAdmin`, `SupabaseStorage`, upload/rate-limit settings).
- [ ] Add migration deployment and rollback runbooks; never auto-run migrations from every scaled API instance.
- [ ] Rehearse migrations against a clean PostgreSQL database and a copy containing representative pre-migration rows.
- [ ] Build and run the container, verify `/health`, `/health/ready`, ProblemDetails, auth, one public endpoint, contact rate limiting, and a Storage flow.
- [ ] Add release checklist and post-deploy verification/rollback criteria.

### API / Events

No new feature endpoints. Operational verification covers all existing endpoints and both health probes.

### Database Impact

No new schema. Rehearse every migration and record forward/rollback procedure, backup requirement, and single-runner ownership.

### Security Review

Non-root runtime, no development Swagger unless explicitly approved, no secrets in image/layers/logs, minimal CI permissions, SHA image tags, and controlled migration credentials.

### Edge Cases

Free-tier cold start, missing secret, database unavailable, failed migration, readiness failure, rollback with newer data, Storage unavailable, and two deployment instances starting simultaneously.

### Unit Tests

No new business unit suite; configuration and script linters may be added where deterministic.

### Integration Tests

Docker build/run smoke test, CI workflow validation, clean/upgrade migration rehearsal, and deployed health/API/Storage smoke test.

### Documentation

Create/update `README.md`, local setup, environment reference, migration runbook, deployment runbook, backup/rollback guide, API/OpenAPI publication, and production verification checklist.

### Definition of Done

```text
dotnet restore                              PASS
dotnet build --configuration Release        PASS
dotnet test --configuration Release         PASS
dotnet format --verify-no-changes            PASS
docker build .                              PASS
container smoke tests                       PASS
migration clean/upgrade rehearsal           PASS
production health and critical flows        PASS
```

---

## 10. Future Phase 2 — Realtime Chat Roadmap (Not Approved MVP Scope)

These sprints satisfy the attachment's coverage requirement without treating absent requirements as permission to implement. Each is blocked until Future Sprint F0 is approved.

### Future Sprint F0 — Chat Specification, Domain Model, and Threat Model

#### Goal

Produce `CHAT_SPEC.md`, `CHAT_DATABASE.md`, REST/WebSocket contracts, capacity assumptions, data retention, moderation policy, and ADRs for host technology, authentication, Redis, and AI.

#### Dependencies

MVP Sprint 10 and an explicit product decision to begin Phase 2.

#### Scope

Guest credential format/rotation; registered account provider; admin identity integration; conversation lifecycle; participant roles; message edit/delete policy; ordering key; idempotency key; image constraints; retention; privacy/export/deletion; online definition; AI consent and data minimization.

#### Tasks

- [ ] Reconcile Phase 2 identity with existing ASP.NET Core Identity and choose whether public accounts share or separate that store.
- [ ] Define REST and WebSocket request, response, acknowledgement, error, retry, and versioning contracts.
- [ ] Define conversation/message state machines and database transaction boundaries.
- [ ] Complete privacy, abuse, capacity, Redis-degradation, and AI data-flow threat models.
- [ ] Review and approve ADRs before creating migrations or runtime projects.

#### API / Events

Documentation only; no runtime routes or events.

#### Database Impact

Design, but do not migrate, `users`, `conversations`, `conversation_participants`, `messages`, guest-identity proof, idempotency uniqueness, and indexes tied to approved queries.

#### Security Review

Never link by matching `guest_email`. Require proof of control of both the opaque guest credential and the verified registered account in one authenticated operation.

#### Edge Cases

Guest credential loss/theft, email reuse, concurrent linking, data deletion/export, conversation closure during send, multiple backend instances, Redis loss, and AI provider outage.

#### Unit Tests

No production unit tests; write executable state-machine examples for every approved transition.

#### Integration Tests

No runtime integration tests; validate schema/API/event examples for consistency and traceability.

#### Documentation

Create the Phase 2 spec, database design, threat model, event catalog, ADRs, capacity assumptions, and operations outline.

#### Definition of Done

Architecture, privacy, threat, schema, API/event, and operational reviews are approved.

### Future Sprint F1 — Signup/Login and Guest Identity

#### Goal

Provide approved public account authentication and a secure guest identity that can later be linked without email-based account takeover.

#### Dependencies

Future Sprint F0.

#### Scope

Public account registration/login if approved; opaque signed guest-session credential; rotation/revocation; verified account linking; abuse controls.

#### Tasks

- [ ] Write failing guest-token and linking security tests.
- [ ] Implement the approved identity/session boundary without trusting client user IDs.
- [ ] Implement one atomic, idempotent link operation requiring guest proof and authenticated verified-account proof.
- [ ] Add login/signup/link throttles, audit events, ProblemDetails, and OpenAPI.

#### API / Events

Use only the route names approved by F0; no WebSocket events are introduced in this sprint.

#### Database Impact

Apply only the approved identity and guest-proof tables/constraints/indexes from `CHAT_DATABASE.md`.

#### Security Review

Email equality alone never grants conversation access. Prevent fixation, replay, enumeration, and brute force; define CSRF behavior if cookies are used.

#### Edge Cases

Valid/expired/tampered guest credentials, verified email change, email collision without proof, replay, concurrent link, and non-owner access.

#### Unit Tests

Test credential validation, link policy, token rotation, and generic failure behavior.

#### Integration Tests

Test signup/login/link endpoints, unique constraints, transaction rollback, concurrent link, rate limits, and authorization.

#### Documentation

Update auth flows, token/cookie storage rules, guest recovery limitations, privacy policy, and OpenAPI.

#### Definition of Done

Guest identity and account linking have executable security evidence and no email-only linking path exists.

### Future Sprint F2 — Conversation and Participant Foundation

#### Goal

Deliver REST-based conversation creation, participant authorization, lifecycle management, and admin conversation management.

#### Dependencies

Future Sprint F1.

#### Scope

Create/read authorized conversations, guest/registered/admin/bot participant roles, future-ready many-participant schema, status transitions, pagination, and admin management.

#### Tasks

- [ ] Write participant authorization and lifecycle tests first.
- [ ] Implement feature-specific service/repository boundaries and REST controllers.
- [ ] Enforce membership/role checks in services and efficient existence checks in repositories.
- [ ] Add stable cursor pagination, admin filters, audit logs, and OpenAPI.

#### API / Events

Implement only F0-approved conversation REST routes. No WebSocket events yet.

#### Database Impact

Constraints and indexes must support participant authorization, admin inbox order, and conversation pagination. PostgreSQL remains the source of truth.

#### Security Review

Prevent IDOR, role escalation, participant enumeration, unauthorized reopen/close/delete, and client-assigned admin/bot roles.

#### Edge Cases

Duplicate participant, linked guest/account identity, closed/deleted conversation, concurrent status transitions, and pagination while new conversations arrive.

#### Unit Tests

Test participant-only access, guest ownership, status transitions, duplicate participant conflict, and admin-only actions.

#### Integration Tests

Test FK/unique/check constraints, authorization joins, pagination stability, concurrent transitions, and REST ProblemDetails.

#### Documentation

Document participant roles, lifecycle diagram, REST contracts, authorization matrix, and data-retention behavior.

#### Definition of Done

Conversations work securely over REST before realtime delivery is introduced.

### Future Sprint F3 — Persistent Messaging and Image Messages

#### Goal

Deliver durable, idempotent text and image messaging over REST before adding realtime transport.

#### Dependencies

Future Sprint F2 and the proven MVP storage boundary.

#### Scope

Persist text/image messages, server-generated ordering, client idempotency key, authorized history pagination, image upload/compensation, and admin responses.

#### Tasks

- [ ] Write duplicate, ordering, authorization, history, and partial-failure tests first.
- [ ] Implement message service/repository boundaries and approved REST routes.
- [ ] Implement server ordering and a unique `(conversation_id, sender_id, client_message_id)` or F0-approved idempotency constraint.
- [ ] Reuse file validation/storage compensation for image messages.

#### API / Events

Implement only F0-approved send/history REST routes. No WebSocket events yet.

#### Database Impact

Apply approved message, attachment, ordering, idempotency, and history-pagination schema/indexes.

#### Security Review

Validate conversation membership server-side; scan/validate images; never trust sender/participant IDs; cap text and attachment sizes.

#### Edge Cases

Duplicate send retry, ordering under concurrency, DB success/storage failure and inverse, unauthorized send/read, closed conversation, malformed image, and stable pagination.

#### Unit Tests

Test message validation, membership, idempotency, closed-state behavior, and compensation policy.

#### Integration Tests

Test unique idempotency, concurrent ordering, authorized history SQL, cursor pagination, Storage failures, and rollback/compensation.

#### Documentation

Document message schema, idempotency contract, attachment lifecycle, ordering, history pagination, and retry rules.

#### Definition of Done

Messaging is correct and recoverable over REST; no WebSocket is needed to prove persistence.

### Future Sprint F4 — Realtime WebSocket Gateway

#### Goal

Add authenticated realtime delivery as an acceleration layer over the persistent messaging service.

#### Dependencies

Future Sprint F3.

#### Scope

Authenticated socket handshake, authorized room join/leave, send/ack events delegating to messaging services, reconnect catch-up from DB, and admin multi-tab behavior.

#### Tasks

- [ ] Write socket authentication, authorization, acknowledgement, reconnect, and failure-ordering tests first.
- [ ] Implement the F0-selected .NET realtime gateway with no core business logic.
- [ ] Persist through the existing message service before broadcasting.
- [ ] Implement reconnect reconciliation from durable history/idempotency state.

#### API / Events

Implement only the versioned event names and payloads approved in F0; preserve REST history as recovery.

#### Database Impact

No schema beyond F3 unless F0 explicitly requires connection/session audit metadata. WebSocket delivery state is not message durability.

#### Security Review

Authenticate handshake and revalidate room authorization; cap event sizes/rates; prevent arbitrary room joins and sender spoofing.

#### Edge Cases

Reconnect, duplicate events, DB success/broadcast failure, broadcast/ack failure, disconnect during send, ordering, token expiry, and multi-tab admin.

#### Unit Tests

Test gateway delegation, authorization decisions, event mapping, and acknowledgement outcomes with mocked transport only.

#### Integration Tests

Test real gateway handshake/events, unauthorized rooms, reconnect catch-up, duplicate retry, failure injection, and concurrent clients.

#### Documentation

Publish the event catalog, sequence diagrams, acknowledgement/retry contract, reconnect algorithm, and delivery guarantees.

#### Definition of Done

Realtime is an acceleration layer over durable message history, never the source of truth.

### Future Sprint F5 — Presence and Redis Scale-Out

#### Goal

Provide explicitly approximate presence and multi-instance realtime fan-out after load evidence justifies Redis.

#### Dependencies

Future Sprint F4 plus an approved multi-instance capacity need.

#### Scope

Online/offline semantics, heartbeat/TTL, multi-tab reference counting, Redis adapter selected for the .NET host, Pub/Sub, and degraded single-instance behavior.

#### Tasks

- [ ] Establish a load baseline proving the need for scale-out.
- [ ] Write TTL, multi-tab, duplicate Pub/Sub, restart, and two-instance tests.
- [ ] Implement an `IPresenceService` and transport backplane adapter behind abstractions.
- [ ] Add health, metrics, timeouts, reconnect, and documented degraded behavior.

#### API / Events

Use F0-approved presence events; presence snapshots include freshness/expiry semantics.

#### Database Impact

No durable online flag. Redis holds ephemeral TTL state; PostgreSQL continues to hold durable conversation/message data.

#### Security Review

Authorize presence subscriptions, minimize exposed metadata, bound keys/TTL/cardinality, secure Redis transport/credentials, and prevent client-set presence for other users.

#### Edge Cases

Abrupt disconnect, expired heartbeat, multiple tabs/devices, Redis restart/unavailable, duplicate Pub/Sub delivery, clock skew, and network partition.

#### Unit Tests

Test TTL state transitions, reference counting, authorization, duplicate events, and degraded-mode policy.

#### Integration Tests

Test two API instances against Redis, fail/restart Redis, reconnect clients, and verify durable chat remains available.

#### Documentation

Document presence accuracy, Redis topology/configuration, key TTLs, metrics, failure modes, and capacity evidence.

#### Definition of Done

Presence is explicitly approximate, Redis failure behavior is documented/tested, and persisted chat remains available.

### Future Sprint F6 — AI Bot Auto-Reply

#### Goal

Add provider-neutral, idempotent AI auto-replies that never block or weaken human messaging.

#### Dependencies

Future Sprint F3; Future Sprint F5 when the approved policy suppresses bots while an admin is online.

#### Scope

`IBotProvider`, provider adapter, bounded prompt/context builder, reply idempotency, timeout/rate-limit/outage handling, admin-presence policy, and safe background processing.

#### Tasks

- [ ] Write timeout, outage, duplication, race, closure, cancellation, and data-minimization tests first.
- [ ] Define provider-neutral request/reply/error contracts and one approved provider adapter.
- [ ] Persist bot jobs/replies idempotently through normal messaging rules.
- [ ] Add bounded context, redaction, consent, metrics, cost/rate limits, and kill switch.

```csharp
public interface IBotProvider
{
    Task<BotReply> GenerateReplyAsync(BotRequest request, CancellationToken cancellationToken);
}
```

#### API / Events

Reuse normal message events for accepted bot replies; expose only F0-approved admin controls/health, never provider secrets.

#### Database Impact

Add only approved idempotent bot-job/reply metadata and indexes tied to pending-job processing; do not store raw prompts unless the privacy policy explicitly requires it.

#### Security Review

Minimize personal data sent to providers, obtain required consent, redact secrets, enforce retention policy, and never let provider output bypass normal message validation.

#### Edge Cases

Timeout, provider 429/5xx, invalid response, duplicate job/reply, admin comes online while pending, conversation closes/deletes, cancellation, and sensitive-context exclusion.

#### Unit Tests

Test provider translation, context bounding/redaction, idempotency, admin-presence race, closed conversation, and provider failure policy.

#### Integration Tests

Test background job claiming/retry, duplicate suppression, persisted reply/broadcast path, provider stub failures, and kill switch.

#### Documentation

Document provider configuration, consent/data flow, prompt/context policy, retry/cost limits, outage behavior, metrics, and disable procedure.

#### Definition of Done

Provider failure never blocks human chat, duplicate replies are prevented, and business logic depends only on `IBotProvider`.

## 11. Feature Coverage Matrix

| Requested capability | Roadmap disposition |
| --- | --- |
| Public portfolio/timeline equivalent | MVP Sprints 2-7; modeled as Profile/About/Skills/Experience/Projects/Certificates/CV, not an invented `timeline_entries` table |
| Timeline owner CRUD equivalent | MVP Sprints 2-7 admin CRUD under single-portfolio decision |
| CV upload/versioning/current download | MVP Sprint 7 |
| Signup/login | Admin login exists; public signup is gated Future Sprint F1 |
| Guest realtime chat | Future Sprints F1-F4 |
| Guest-to-registered linking | Future Sprint F1 with proof-of-control, never email matching |
| Admin conversation management | Future Sprint F2 |
| Realtime messaging | Future Sprint F4 |
| Online/offline presence | Future Sprint F5 |
| Image messages | Future Sprint F3 |
| Future-ready participants | Future Sprint F2 |
| AI bot auto-reply | Future Sprint F6 |
| Redis adapter | Future Sprint F5 only after demonstrated scale need |
| Swagger/OpenAPI | Every MVP feature sprint; release publication in Sprint 10 |
| Security/RLS | Decisions in Sprint 0, implementation throughout, proof in Sprint 9 |
| Automated tests | Every sprint |
| Operational documentation | Every sprint, finalized in Sprint 10 |

## 12. Unit-Test Strategy

- Use xUnit v3 and AAA.
- Controller tests mock `IService` only when an HTTP-specific branch cannot be covered more clearly by `WebApplicationFactory`.
- Service tests use small hand-written stubs/fakes or an approved mocking package only at repository/storage/notifier boundaries.
- Validator and pure-policy tests are table-driven for locale, enum, URL, date, file, publish, disclosure, and status-transition cases.
- Every business method covers happy path, validation failure, not found, conflict/authorization where applicable, dependency failure, cancellation, and important edge cases.
- Tests assert stable exception/error codes rather than fragile full prose where the contract defines codes.
- New regression lessons are added to `docs/BUG_LESSONS.md` only after a root cause and fix are verified.

## 13. Integration-Test Strategy

- Use one PostgreSQL Testcontainer collection fixture for repository/migration tests; do not use EF InMemory as proof of PostgreSQL behavior.
- Apply real migrations, not `EnsureCreated`, in migration and API/database suites.
- Test constraints, partial unique indexes, FK delete behavior, transactions, query translation, ordering, pagination, and concurrency.
- Use `WebApplicationFactory<Program>` for serialization, routing, authorization, ProblemDetails, CORS, rate-limit, and multipart behavior.
- Stub the Storage HTTP endpoint with a deterministic local handler for fast contract tests; run a separately configured Supabase staging smoke test outside the unit suite.
- Never require live external services for unit tests or ordinary PR verification.
- Record generated SQL for only the non-trivial public list/detail projections during review; avoid snapshotting provider-noisy SQL unless it catches a real regression.

## 14. Documentation Plan

| Document | Created/updated in |
| --- | --- |
| ADR for MVP decisions | Sprint 0 |
| REST API contract and examples | Sprint 0, then every feature sprint |
| Threat model/security checklist | Sprint 0 and Sprint 9 |
| Test strategy | Sprint 0 and Sprint 1 |
| Environment/configuration reference | Sprint 1 and Sprint 10 |
| Storage buckets, policies, object keys, compensation | Sprint 1 and file-feature sprints |
| Feature business rules and admin/public DTOs | Sprints 2-8 |
| Disclosure matrix | Sprint 5 |
| Resume versioning/transaction runbook | Sprint 7 |
| Contact abuse/privacy/retention | Sprint 8 |
| Supabase database/Storage access policy | Sprint 9 |
| OpenAPI | Every endpoint sprint |
| Local development, Docker, CI, deploy, migration, rollback | Sprint 10 |
| Phase 2 chat spec/events/ADRs | Future Sprint F0 |

## 15. Global Definition of Done

A sprint is complete only when:

- its API contract and DTO validation are implemented exactly as approved;
- controller, service, repository, and Infrastructure responsibilities remain separated;
- authorization, publication, disclosure, and ownership/single-portfolio assumptions are tested;
- transaction and storage-compensation boundaries are reviewed and tested;
- migration, constraint, index, RLS/access-policy, and rollback impact are documented;
- ProblemDetails, structured logging, redaction, and cancellation propagation are preserved;
- concrete unit, PostgreSQL integration, API, security, and regression tests pass as applicable;
- Swagger/OpenAPI and feature documentation are synchronized;
- relevant `docs/BUG_LESSONS.md` entries were checked and any new verified recurring bug was recorded;
- Release build passes with zero warnings; formatting and Docker checks pass when relevant;
- no secret, signed URL, raw file content, password, JWT, service key, or connection string appears in source or logs.

## 16. Recommended Implementation Order and Review Gates

Execute Sprints 0 through 10 in order, with Skills and Profile/About allowed in parallel only after Sprint 1 is complete and their database migrations do not overlap. Review and merge one feature sprint before beginning a dependent sprint. Use small commits at red test, green implementation, and documentation/refactor boundaries when each commit builds independently.

Do not begin Future Sprint F1 until F0 is approved as a separate project. The future phase changes authentication, domain model, data privacy, database schema, deployment topology, and operational cost; folding it into the portfolio MVP would violate the repository's architecture and scope controls.

## 17. Plan Self-Review

- **Spec coverage:** BE-01 through BE-09, shared architecture, validation, error handling, Storage, logging, CORS, configuration, Docker, CI, migration, testing, deployment, and Definition of Done map to Sprints 0-10.
- **Attachment coverage:** All 18 listed capabilities plus guest linking, realtime failure modes, presence, Redis, image messages, and AI provider behavior map to Future Sprints F0-F6.
- **Placeholder scan:** No production task relies on an unspecified placeholder. Unapproved contracts are explicit Sprint 0/F0 gates rather than guessed implementation instructions.
- **Type consistency:** All file-storage tasks use the single `IFileStorage`/`StorageUpload`/`StorageObject` boundary. AI work uses `IBotProvider` only in the separately gated future phase.
- **Architecture consistency:** No Domain project, generic repository, custom unit of work, CQRS, or MediatR is introduced.
