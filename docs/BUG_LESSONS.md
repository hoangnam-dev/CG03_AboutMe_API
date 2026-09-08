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
| BUG-2026-006 | Resolved | Other | Shared | Mermaid semicolon split sequence statements | `documentation`, `mermaid`, `parser`, `diagram-validation` |
| BUG-2026-007 | Resolved | API | About | Debugger stop on handled NotFound looked like a process crash | `exception-handling`, `problem-details`, `debugger`, `not-found` |

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

## BUG-2026-006 — Mermaid semicolon split sequence statements

- **Status:** Resolved
- **Area:** Other
- **Feature:** Shared
- **First observed:** 2026-09-08
- **Last updated:** 2026-09-08
- **Tags:** `documentation`, `mermaid`, `parser`, `diagram-validation`

### Symptom

The Sprint 5 public Project sequence diagram failed to parse immediately after a `Note over` line. The same parser failure then recurred in both login and refresh-rotation diagrams in `AUTHENTICATION_GUIDE.md`.

### Root cause

Raw semicolons appeared inside Mermaid sequence-diagram note/message text. The Mermaid parser treated each semicolon as a statement separator and parsed the remaining prose as a new, invalid diagram statement.

### Why it happened

Verification checked Markdown fence counts and balanced `alt`/`loop`/`opt` blocks but did not run a Mermaid parser. Those structural checks cannot detect tokenization errors inside otherwise balanced diagrams.

### Correct fix

Replace raw semicolons in Mermaid note/message text with commas or conjunctions, then scan every Mermaid block for the same pattern and render with a real Mermaid parser when one is available.

### Prevention rule

Do not place raw `;` characters inside Mermaid sequence-diagram note or message text. Before declaring a flow document valid, run a real Mermaid parse/render; structural fence and control-block checks are supplementary only.

### Regression test

One-off repository-wide scan of every Mermaid `sequenceDiagram` rejects raw semicolons. A permanent parser-backed test was not added because this repository does not currently include a Mermaid CLI/runtime dependency.

### Verification

- The pre-fix scan found unsafe semicolons at lines 858 and 946 of `CURRENT_PROCESS_FLOWS.md` and failed.
- The same scan passed after both statements were rewritten.
- A repository-wide recurrence scan later found eight unsafe semicolons in the login and refresh-rotation diagrams of `AUTHENTICATION_GUIDE.md`; the scan passed after all eight statements were rewritten.

### Relevant files

- `docs/architecture/CURRENT_PROCESS_FLOWS.md`
- `docs/security/AUTHENTICATION_GUIDE.md`
- `docs/BUG_LESSONS.md`

### Related lessons

- None.

---

## BUG-2026-007 — Debugger stop on handled NotFound looked like a process crash

- **Status:** Resolved
- **Area:** API
- **Feature:** About
- **First observed:** 2026-09-08
- **Last updated:** 2026-09-08
- **Tags:** `exception-handling`, `problem-details`, `debugger`, `not-found`

### Symptom

Visual Studio stopped at `AboutService.GetPublicAsync` when published About data was absent and displayed `Exception User-Unhandled` for `NotFoundException`. While IIS Express was paused, Bruno could not receive the pending 404 response and eventually displayed `REQUEST_CANCELED`, which looked like the API process had crashed.

### Root cause

The repository correctly returned `null` when the Profile, About row, requested translation, or published state did not satisfy the public query. `AboutService` intentionally converted that absence into `NotFoundException`, and `GlobalExceptionHandler` correctly mapped it to HTTP 404 ProblemDetails. Visual Studio had `Break when this exception type is user-unhandled` enabled. Because the exception leaves Application/controller user code before ASP.NET Core's framework pipeline invokes `IExceptionHandler`, Just My Code classified the intermediate state as user-unhandled and paused execution even though the HTTP boundary handles it.

### Why it happened

Debugger exception settings and the exception helper displayed the throw site before centralized middleware completed the request. Pausing the server also kept the HTTP connection pending, so the client-side cancellation was incorrectly interpreted as server termination.

### Correct fix

Keep centralized exception mapping and do not add repetitive controller `try/catch` blocks. In the exception popup, clear `Break when this exception type is user-unhandled` for `NotFoundException` and press Continue/F5. Alternatively add the shown `Portfolio.Application.dll` exclusion if the developer wants to preserve the general user-unhandled rule. Then resend the request if the client already canceled it. Seed and publish the required About translations when a 200 response is expected.

### Prevention rule

When a known application exception appears under a debugger, verify the final HTTP status, ProblemDetails body, and server liveness before treating it as a crash or changing exception flow. Public missing/draft/translation-absent queries must remain 404, not 500 and not an empty 200 response.

### Regression test

`tests/Portfolio.IntegrationTests/Api/ProfileAboutApiTests.cs` — `MissingPublishedAboutReturnsNotFoundWithoutStoppingServer` asserts 404 ProblemDetails and then verifies `/health` still returns 200.

### Verification

- Focused HTTP regression test passed: 1 succeeded, 0 failed.
- Source inspection confirmed `GlobalExceptionHandler` maps `NotFoundException` to 404 and logs only unexpected 500 exceptions as unhandled.
- Visual Studio evidence showed `Break when this exception type is thrown` disabled and `Break when this exception type is user-unhandled` enabled; Bruno showed `REQUEST_CANCELED` while IIS Express remained paused.

### Relevant files

- `src/Portfolio.Application/About/AboutService.cs`
- `src/Portfolio.Infrastructure/Persistence/Repositories/AboutRepository.cs`
- `src/Portfolio.Api/Errors/GlobalExceptionHandler.cs`
- `tests/Portfolio.IntegrationTests/Api/ProfileAboutApiTests.cs`
- `docs/architecture/CURRENT_PROCESS_FLOWS.md`

### Related lessons

- None.

---

## BUG-2026-008 — Swagger generated invalid keys for a locale dictionary

- **Status:** Resolved
- **Area:** API
- **Feature:** Shared (Profile, About, Skills, Experiences, Projects, Certificates)
- **First observed:** 2026-09-08
- **Last updated:** 2026-09-08
- **Tags:** `swagger`, `openapi`, `dictionary`, `localization`, `validation`

### Symptom

Swagger UI generated `additionalProp1`, `additionalProp2`, and `additionalProp3` under `translations`. The first fix covered only `PUT /api/v1/admin/about`; Profile, Skills, Experiences, Projects, and Certificates continued to advertise invalid payloads even though every feature accepts only `en` and `vi`. Other JSON request bodies also relied on generic Swagger placeholders instead of contract-valid examples.

### Root cause

The localized write DTOs expose `Translations` as `IReadOnlyDictionary<string, TTranslation>`. The CLR type permits arbitrary string keys, so generated OpenAPI cannot infer the application's finite locale set and Swagger UI displays generic dictionary placeholders. Swashbuckle also does not invent domain-valid values for dates, URLs, enum-like strings, related IDs, or nested collection rules.

### Why it happened

Runtime validation constrained the keys, but OpenAPI documentation did not express or override that business rule. The initial operation filter was tied specifically to `AboutUpdateRequest`, so it corrected one symptom without auditing the same DTO pattern across the other controllers.

### Correct fix

Register one centralized `RequestExampleOperationFilter` that selects a literal, contract-valid example from the controller request parameter type. Cover every implemented JSON request operation and use explicit `translations.en`/`translations.vi` objects. Keep runtime validation as the enforcement boundary.

### Prevention rule

Whenever a new JSON request body is added, add its contract-valid example to the centralized filter and regression matrix. For dictionaries restricted by business rules, use explicit valid keys and recursively reject `additionalProp*`; do not add one-off feature filters for a shared failure pattern.

### Regression test

`tests/Portfolio.IntegrationTests/Api/ApiFoundationTests.cs` — `SwaggerEveryJsonRequestBodyHasContractExample` enumerates all implemented `application/json` operations, verifies exact root request fields, requires `en`/`vi` translation keys, and recursively rejects `additionalProp*`. The original `SwaggerAboutUpdateExampleUsesSupportedLocales` test remains as focused About coverage.

### Verification

- Before the fix, the repository-wide regression test failed because most JSON media types had no explicit `example`.
- After the fix, the focused repository-wide Swagger regression test passed: 1 succeeded, 0 failed.
- The non-PostgreSQL integration suite passed: 113 succeeded, 0 failed.
- Unit tests passed: 97 succeeded, 0 failed.

### Relevant files

- `src/Portfolio.Api/OpenApi/RequestExampleOperationFilter.cs`
- `src/Portfolio.Api/Extensions/ServiceCollectionExtensions.cs`
- `tests/Portfolio.IntegrationTests/Api/ApiFoundationTests.cs`
- `tests/Portfolio.IntegrationTests/Api/ProfileAboutApiTests.cs`

### Related lessons

- BUG-2026-006

---

## BUG-2026-009 — Certificate upload replaced the wrong evidence slot

- **Status:** Resolved
- **Area:** Application | Storage
- **Feature:** Certificates
- **First observed:** 2026-09-08
- **Last updated:** 2026-09-08
- **Tags:** `file-replacement`, `object-storage`, `partial-failure`, `contract-mapping`

### Symptom

Uploading a PDF certificate evidence file cleared and deleted an existing certificate image, even though the schema and API contract expose independent file and image fields.

### Root cause

The replacement flow treated `certificates.file_url` and `certificates.image_url` as two representations of one storage slot instead of two independent slots selected by validated content type.

### Why it happened

The singular upload route was mistaken for singular persisted evidence. The content-type rule that updates the appropriate field was not carried through to old-object selection and cleanup.

### Correct fix

PDF uploads replace only `file_url`; PNG/JPEG/WebP uploads replace only `image_url`. Persist the selected new key before deleting only the previous object from that same slot, and sign both retained slots independently in read responses.

### Prevention rule

When one upload endpoint dispatches to multiple storage-backed columns by content type, replace and clean up only the selected column; never clear sibling storage slots unless the contract explicitly defines mutual exclusion.

### Regression test

`tests/Portfolio.UnitTests/Certificates/CertificateServiceTests.cs` — `UploadingPdfPreservesExistingImageEvidence` and `AdminResponseSignsFileAndImageEvidenceIndependently`.

### Verification

- Certificate service tests passed: 9 succeeded, 0 failed.
- Certificate API tests passed: 7 succeeded, 0 failed.
- Release build passed with 0 warnings and 0 errors.

### Relevant files

- `src/Portfolio.Application/Certificates/CertificateService.cs`
- `src/Portfolio.Application/Certificates/CertificateContracts.cs`
- `tests/Portfolio.UnitTests/Certificates/CertificateServiceTests.cs`

### Related lessons

- None.

---

## BUG-2026-010 — Synchronous barrier blocked async concurrency test construction

- **Status:** Resolved
- **Area:** Testing | PostgreSQL
- **Feature:** Resumes
- **First observed:** 2026-09-08
- **Last updated:** 2026-09-08
- **Tags:** `concurrency-test`, `async-deadlock`, `testcontainers`

### Symptom

The Resume counter concurrency test started but never reached PostgreSQL or completed.

### Root cause

`Barrier.SignalAndWait` ran synchronously inside an `async` LINQ selector before the task sequence finished enumerating. The first selector invocation blocked while later participants had not yet been constructed.

### Why it happened

The test assumed invoking an `async` lambda immediately scheduled its whole body independently. In reality, it executes synchronously until its first incomplete `await`.

### Correct fix

Create every task behind an incomplete `TaskCompletionSource`, materialize the task array, and then release the asynchronous start gate.

### Prevention rule

Concurrency tests must use an asynchronous start gate and materialize all participants before releasing it; never place a synchronous blocking barrier before the first incomplete `await` during task construction.

### Regression test

`tests/Portfolio.IntegrationTests/Persistence/ResumeRepositoryTests.cs` — `ConcurrentUploadsAllocateUniqueMonotonicSequences`.

### Verification

- Focused Resume repository tests passed: 5 succeeded, 0 failed.
- Full solution tests passed: 288 succeeded, 0 failed.

### Relevant files

- `tests/Portfolio.IntegrationTests/Persistence/ResumeRepositoryTests.cs`

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
