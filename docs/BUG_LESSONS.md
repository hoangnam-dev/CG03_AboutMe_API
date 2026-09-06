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
