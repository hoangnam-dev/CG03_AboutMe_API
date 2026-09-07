# CG03 Backend Bug Lessons

This document records reusable lessons from bugs encountered while developing
the CG03 AboutMe backend.

Its purpose is regression prevention, not incident narration.

## Usage rules

- Record only bugs with a reusable engineering lesson.
- Confirm root cause before marking an entry `Resolved`.
- Prefer updating an existing lesson over creating duplicates.
- Add a regression test whenever practical.
- Never store secrets, credentials, tokens, or personal data here.
- Keep prevention rules concise and actionable.
- Use `BUG-YYYY-NNN` IDs and never reuse an ID.

## Index

| ID | Status | Area | Feature | Title | Tags |
| --- | --- | --- | --- | --- | --- |
| BUG-2026-001 | Resolved | Testing | Shared | WebApplicationFactory inherited developer service configuration | `webapplicationfactory`, `configuration`, `test-isolation`, `secrets` |
| BUG-2026-002 | Resolved | EF Core | Shared | Migration command loaded stale build output | `ef-core`, `migration`, `no-build`, `git-safety` |
| BUG-2026-003 | Resolved | Auth | Shared | Case-sensitive auth path check allowed Origin bypass | `csrf`, `origin`, `routing`, `case-sensitivity` |
| BUG-2026-004 | Resolved | EF Core | Skills | CLR-default enum was replaced by a database default | `ef-core`, `enum`, `sentinel`, `database-default`, `postgresql` |
| BUG-2026-005 | Resolved | Testing | Shared | PostgreSQL reset omitted a parent table | `test-isolation`, `postgresql`, `truncate`, `shared-fixture` |

---

## BUG-2026-001 — WebApplicationFactory inherited developer service configuration

- **Status:** Resolved
- **Area:** Testing
- **Feature:** Shared
- **First observed:** 2026-09-06
- **Last updated:** 2026-09-06
- **Tags:** `webapplicationfactory`, `configuration`, `test-isolation`, `secrets`

### Symptom

The database-optional API test reported readiness and contacted locally configured Supabase services instead of exercising unavailable-service registrations.

### Root cause

`ConfigureAppConfiguration` test values were applied too late to control configuration branches evaluated while the minimal-host application registered services. Developer configuration had already selected the real database and Storage registrations.

### Why it happened

The test assumed an in-memory configuration provider added through `ConfigureAppConfiguration` governed service-registration decisions in `Program.cs`.

### Correct fix

Set database and Storage configuration through `IWebHostBuilder.UseSetting` in each API factory so the overrides are visible before application services are registered.

### Prevention rule

API factories must override external-service selection settings with `UseSetting` before startup; never rely only on late app-configuration providers to isolate developer secrets and live services.

### Regression test

`tests/Portfolio.IntegrationTests/Api/ApiFoundationTests.cs` — `DatabaseOptionalStartupTests.ApiStartsWithoutDatabaseConfiguration`

### Verification

- `dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --no-restore --filter FullyQualifiedName~DatabaseOptionalStartupTests` — passed.
- Non-persistence integration suite — 45 passed, 0 failed.

### Relevant files

- `tests/Portfolio.IntegrationTests/Api/ApiFoundationTests.cs`

### Related lessons

- None.

---

## BUG-2026-002 — Migration command loaded stale build output

- **Status:** Resolved
- **Area:** EF Core
- **Feature:** Shared
- **First observed:** 2026-09-06
- **Last updated:** 2026-09-07
- **Tags:** `ef-core`, `migration`, `no-build`, `git-safety`

### Symptom

EF Core generated an empty authentication migration even though the model contained new entities and columns. A subsequent unverified `migrations remove` targeted the previously valid Sprint 1 migration instead of only the disposable migration.

### Root cause

The migration command reused stale build output from a different configuration. EF compared the old compiled model with the snapshot, so the generated `Up`/`Down` methods were empty. Removal was then run without first confirming the latest migration name and generated files.

### Why it happened

`--no-build` was treated as safe after building another configuration, and the generated migration was not inspected before the next migration command.

### Correct fix

Restore the untouched Sprint 1 migration/snapshot from Git, build the Infrastructure and startup projects in the same configuration used by `dotnet-ef`, regenerate `AddRefreshTokenSessions`, inspect `Up`, `Down` and the snapshot, then apply it to an empty PostgreSQL Testcontainer.

### Prevention rule

Before adding/removing an EF migration, build the exact selected configuration and verify the latest migration name plus generated `Up`/`Down`; never use `--no-build` against unverified output or run `migrations remove` blindly.

### Regression test

`tests/Portfolio.IntegrationTests/Persistence/MigrationTests.cs` — verifies all three migrations apply to empty PostgreSQL.

### Verification

- `dotnet build Portfolio.sln --configuration Release --no-restore` — passed with 0 warnings/errors.
- PostgreSQL persistence suite with Docker/Testcontainers — 22 passed, 0 failed after regeneration.
- `dotnet-ef migrations list` reports `InitialCreate`, `Sprint1SchemaCorrections`, and `AddRefreshTokenSessions`.

### Relevant files

- `src/Portfolio.Infrastructure/Persistence/Migrations/20260906132748_AddRefreshTokenSessions.cs`
- `src/Portfolio.Infrastructure/Persistence/Migrations/PortfolioDbContextModelSnapshot.cs`
- `tests/Portfolio.IntegrationTests/Persistence/MigrationTests.cs`

### Related lessons

- None.

---

## BUG-2026-003 — Case-sensitive auth path check allowed Origin bypass

- **Status:** Resolved
- **Area:** Auth
- **Feature:** Shared
- **First observed:** 2026-09-07
- **Last updated:** 2026-09-07
- **Tags:** `csrf`, `origin`, `routing`, `case-sensitivity`

### Symptom

`POST /API/V1/AUTH/LOGIN` reached the case-insensitive ASP.NET Core route and returned 200 without an `Origin`, while the lowercase path correctly returned 403.

### Root cause

Endpoint routing matches paths case-insensitively, but `AuthOriginValidationMiddleware` guarded `/api/v1/auth` with `StringComparison.Ordinal`. Changing path casing therefore skipped both Origin validation and auth no-cache headers.

### Why it happened

The middleware path predicate did not use the same case semantics as ASP.NET Core routing, and tests covered only canonical lowercase URLs.

### Correct fix

Use `StringComparison.OrdinalIgnoreCase` for every auth path predicate in the middleware and add an HTTP regression test with uppercase route segments.

### Prevention rule

Security middleware path matching must be at least as permissive as endpoint routing; for ASP.NET Core route paths, use case-insensitive matching and test non-canonical casing.

### Regression test

`tests/Portfolio.IntegrationTests/Api/AuthApiTests.cs` — `OriginProtectionCannotBeBypassedWithRouteCasing`.

### Verification

- The regression test returned 200 before the fix and 403 after the fix.
- `dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --no-restore --filter FullyQualifiedName~OriginProtectionCannotBeBypassedWithRouteCasing` — passed.

### Relevant files

- `src/Portfolio.Api/Authentication/AuthOriginValidationMiddleware.cs`
- `tests/Portfolio.IntegrationTests/Api/AuthApiTests.cs`

### Related lessons

- None.

---

## BUG-2026-004 — CLR-default enum was replaced by a database default

- **Status:** Resolved
- **Area:** EF Core
- **Feature:** Skills
- **First observed:** 2026-09-07
- **Last updated:** 2026-09-07
- **Tags:** `ef-core`, `enum`, `sentinel`, `database-default`, `postgresql`

### Symptom

Saving a new Lucide skill failed PostgreSQL check constraint `ck_technologies_icon_value`; the value `database` was validated as though its icon type were `text`.

### Root cause

`SkillIconType.Lucide` has CLR enum value `0`. EF Core used the CLR default as the sentinel for the store-generated `icon_type` property, omitted the explicitly selected Lucide value on INSERT, and PostgreSQL applied the column default `text`.

### Why it happened

The EF mapping declared a database default without assigning a sentinel outside the valid domain enum values.

### Correct fix

Keep the database default and configure EF with `(SkillIconType)(-1)` as its sentinel so every valid icon type, including enum value `0`, is sent explicitly.

### Prevention rule

When an EF property has a database default and CLR default is a valid explicit domain value, configure a sentinel outside the valid domain or prove with a persistence test that EF sends the CLR-default value.

### Regression test

`tests/Portfolio.IntegrationTests/Persistence/SkillRepositoryTests.cs` — `PersistsLucideInsteadOfApplyingTextDatabaseDefault`.

### Verification

- The focused PostgreSQL test failed with constraint `ck_technologies_icon_value` before the fix and passed after the sentinel was configured.
- `dotnet test Portfolio.sln --configuration Release --no-build` with Docker API 1.43 — 188 passed, 0 failed.

### Relevant files

- `src/Portfolio.Infrastructure/Persistence/Configurations/SkillConfigurations.cs`
- `tests/Portfolio.IntegrationTests/Persistence/SkillRepositoryTests.cs`

### Related lessons

- None.

---

## BUG-2026-005 — PostgreSQL reset omitted a parent table

- **Status:** Resolved
- **Area:** Testing
- **Feature:** Shared
- **First observed:** 2026-09-07
- **Last updated:** 2026-09-07
- **Tags:** `test-isolation`, `postgresql`, `truncate`, `shared-fixture`

### Symptom

An Experience repository test expected two rows after reset but read rows created by earlier tests in the same PostgreSQL collection.

### Root cause

`ResetApplicationDataAsync` truncated all Experience child tables but omitted the parent `work_experiences` table.

### Why it happened

The shared reset list was updated for the child tables without verifying that every mutable application table, including its aggregate root, was present.

### Correct fix

Add `work_experiences` to the fixture's single `TRUNCATE ... RESTART IDENTITY CASCADE` statement.

### Prevention rule

Whenever a persisted aggregate becomes test-active, verify the shared PostgreSQL reset truncates its root table as well as child tables, and prove isolation with sequential repository tests.

### Regression test

`tests/Portfolio.IntegrationTests/Persistence/ExperienceRepositoryTests.cs` — the class-level sequence, especially `ReorderRejectsPartialSetWithoutChangingOrders`, detects leaked Experience rows.

### Verification

- The focused suite failed with leaked rows before the fix.
- `dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --no-restore --filter FullyQualifiedName~ExperienceRepositoryTests` with Docker API 1.43 — 5 passed, 0 failed after the fix.

### Relevant files

- `tests/Portfolio.IntegrationTests/Infrastructure/PostgreSqlFixture.cs`
- `tests/Portfolio.IntegrationTests/Persistence/ExperienceRepositoryTests.cs`

### Related lessons

- None.

---

## Entry template

<!--
Copy the section below when recording a new bug.

## BUG-YYYY-NNN — Short title

- **Status:** Resolved | Investigating | Superseded
- **Area:** API | Application | Repository | EF Core | PostgreSQL | Auth | Storage | Testing | Docker | CI/CD | Other
- **Feature:** Projects | Skills | Experiences | Certificates | Resumes | Contacts | Shared | Other
- **First observed:** YYYY-MM-DD
- **Last updated:** YYYY-MM-DD
- **Tags:** `tag-1`, `tag-2`

### Symptom

Describe what was observed.

### Root cause

Describe the confirmed technical cause.

### Why it happened

Describe the incorrect assumption, missing guardrail, framework behavior, or
implementation mistake that allowed the bug.

### Correct fix

Describe the minimal reliable fix.

### Prevention rule

Write one concise, actionable rule future implementations must follow.

### Regression test

Give the test name/path, or write `Not added` and explain why.

### Verification

List only verification actually performed.

### Relevant files

- `path/to/file.cs`

### Related lessons

- BUG-YYYY-NNN
-->
