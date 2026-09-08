# Sprint 9 Security Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the MVP security release gate with executable cross-feature authorization, disclosure, configuration, logging, database-access, and Storage-policy evidence.

**Architecture:** Preserve the API as the only application authorization boundary. Add production-only validation beside existing options validation, exercise the real middleware/endpoint metadata through `WebApplicationFactory`, keep audit events in application services, and deliver Supabase role and bucket controls as reviewed operational artifacts rather than domain migrations.

**Tech Stack:** .NET 10, ASP.NET Core 10, xUnit v3, EF Core 10/Npgsql, Serilog source-generated logging, PostgreSQL/Supabase SQL and dashboard policy configuration.

**Spec:** `docs/superpowers/plans/2026-09-05-personal-portfolio-backend-roadmap.md` — Sprint 9; `docs/security/THREAT_MODEL.md`; `docs/adr/0001-mvp-contract-decisions.md` GATE-13/GATE-14.

## Global Constraints

- Keep the existing `Portfolio.Api -> Portfolio.Application -> repository abstraction -> Portfolio.Infrastructure` dependency direction.
- The production EF role is server-only; Supabase `anon` and `authenticated` roles receive no application-table access.
- `avatars`, `project-images`, and `skill-icons` are public-read/server-write; `certificate-files` and `cv-files` are private/server-write.
- Never mutate rows in the managed `storage` schema directly.
- Production accepts exactly one absolute HTTPS frontend origin and cannot start with bootstrap administration enabled.
- Logs contain only approved scalar identifiers/state; never log content, credentials, tokens, signed URLs, storage service keys, or connection strings.
- Domain schema changes are out of scope; policy SQL must include rollback guidance.

---

### Task 1: Production security validation

**Files:**
- Create: `src/Portfolio.Api/Configuration/ProductionFrontendOptionsValidator.cs`
- Create: `src/Portfolio.Infrastructure/Authentication/ProductionBootstrapAdminOptionsValidator.cs`
- Modify: `src/Portfolio.Api/Extensions/ServiceCollectionExtensions.cs`
- Modify: `src/Portfolio.Infrastructure/DependencyInjection.cs`
- Test: `tests/Portfolio.IntegrationTests/Authentication/ProductionSecurityOptionsValidatorTests.cs`

**Interfaces:**
- Consumes: `FrontendOptions`, `BootstrapAdminOptions`, and `IHostEnvironment`.
- Produces: two `IValidateOptions<T>` implementations registered in production and skipped by the existing Development fallback.

- [ ] **Step 1: Write failing validator tests** asserting production rejects HTTP/multiple frontend origins and enabled bootstrap administration, while Development keeps its localhost/multi-origin workflow.
- [ ] **Step 2: Run** `dotnet test tests\Portfolio.IntegrationTests\Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~ProductionSecurityOptionsValidatorTests` and confirm the new production validator types/behavior are absent.
- [ ] **Step 3: Implement the validators** so `ProductionFrontendOptionsValidator.Validate` requires one HTTPS origin and `ProductionBootstrapAdminOptionsValidator.Validate` requires `Enabled == false` outside Development.
- [ ] **Step 4: Register both validators** without weakening existing general validators or Development optional-dependency behavior.
- [ ] **Step 5: Re-run the focused tests** and require zero failures.

### Task 2: Cross-feature authorization and disclosure proof

**Files:**
- Create: `tests/Portfolio.IntegrationTests/Security/AdminAuthorizationTests.cs`
- Create: `tests/Portfolio.IntegrationTests/Security/PublicLeakTests.cs`
- Modify: `tests/Portfolio.IntegrationTests/Api/ProfileAboutApiTests.cs`
- Verify: existing Project, Certificate, Resume, Contact, and repository disclosure tests

**Interfaces:**
- Consumes: the real controller endpoint metadata, JWT middleware, `DatabaseOptionalApiFactory`, and public response JSON contracts.
- Produces: a table-driven route matrix for every `/api/v1/admin` HTTP operation and field-level leak assertions for public DTOs.

- [ ] **Step 1: Add a dynamic admin endpoint matrix** that enumerates all controller endpoints, materializes constrained route parameters with fixed fictional values, and sends the declared HTTP method.
- [ ] **Step 2: Assert the matrix** returns 401 for anonymous callers, 403 for authenticated non-admin callers, and any non-401/non-403 routed result for Administrator callers.
- [ ] **Step 3: Run** `dotnet test tests\Portfolio.IntegrationTests\Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~AdminAuthorizationTests` and fix only genuine authorization/routing gaps.
- [ ] **Step 4: Complete public serialization coverage** by adding the missing hidden Profile contact HTTP case and retaining the existing limited Project, hidden Certificate credential, private Resume object-key, Contact receipt/inbox, draft, and locale tests.
- [ ] **Step 5: Run the authorization matrix and the full integration suite** and require zero failures.

### Task 3: Complete audit events and redaction evidence

**Files:**
- Modify: `src/Portfolio.Application/Resumes/ResumeService.cs`
- Modify: `tests/Portfolio.UnitTests/Resumes/ResumeServiceTests.cs`
- Modify: `tests/Portfolio.UnitTests/Resumes/ResumeServiceTests.cs` with a focused recording logger

**Interfaces:**
- Consumes: existing Resume current/publish use cases and `ILogger<ResumeService>`.
- Produces: source-generated event 2704 for current-selection and 2705 for publication changes, containing only Resume ID and boolean state.

- [ ] **Step 1: Write failing Resume audit tests** that execute current/publish operations and assert event ID, resource ID/state, and absence of descriptions, filenames, URLs, tokens, and secrets.
- [ ] **Step 2: Run** `dotnet test tests\Portfolio.UnitTests\Portfolio.UnitTests.csproj --configuration Release --filter FullyQualifiedName~ResumeServiceTests` and confirm the audit assertions fail because the events are missing.
- [ ] **Step 3: Add minimal source-generated events** after successful persistence/activation only.
- [ ] **Step 4: Re-run Resume tests** and require zero failures.

### Task 4: Supabase policy artifacts and release-gate closure

**Files:**
- Create: `docs/supabase/database-access.sql`
- Create: `docs/supabase/database-access-rollback.sql`
- Create: `docs/supabase/STORAGE_ACCESS.md`
- Create: `docs/security/SPRINT_9_SECURITY_CHECKLIST.md`
- Modify: `docs/security/THREAT_MODEL.md`

**Interfaces:**
- Consumes: ADR GATE-13/GATE-14, current EF table set, and current Storage bucket names.
- Produces: least-privilege role/grant SQL, explicit rollback, bucket access matrix/dashboard steps, smoke-test commands, secret rotation and incident diagnostics, and SEC-01..SEC-12 evidence mapping.

- [ ] **Step 1: Write database access SQL** that revokes application-schema access from `anon`/`authenticated`, grants only runtime DML to `portfolio_api`, reserves DDL/ownership for `portfolio_migrator`, and configures future default privileges.
- [ ] **Step 2: Write rollback SQL** that removes Sprint 9 role memberships/grants and documents restoration of captured pre-deployment grants without granting broad Data API access by default.
- [ ] **Step 3: Write the Storage runbook** for three public-read/server-write buckets and two private/server-write buckets, with no client write policies and no direct managed-schema mutation.
- [ ] **Step 4: Add the security checklist** mapping SEC-01..SEC-12 to named automated tests or an explicit production smoke owner; include secret rotation, policy-before-app deployment, incident diagnostics, and accepted-risk ownership.
- [ ] **Step 5: Update the threat model** with links to executable/operational evidence and an honest statement that live Supabase smoke checks remain an environment-owned release action.

### Task 5: Full verification

**Files:**
- Modify only files found by formatting that were changed in Tasks 1-4.

**Interfaces:**
- Consumes: all Sprint 9 changes.
- Produces: fresh build/test/format evidence and a documented live-provider verification boundary.

- [ ] **Step 1: Run** `dotnet restore`.
- [ ] **Step 2: Run** `dotnet build --configuration Release --no-restore`.
- [ ] **Step 3: Run** `dotnet test --configuration Release --no-build`.
- [ ] **Step 4: Run** `dotnet format --verify-no-changes` and resolve only Sprint 9 formatting issues.
- [ ] **Step 5: Re-check** `docs/BUG_LESSONS.md` for authentication, configuration, Storage, and disclosure prevention rules and confirm their named regressions remain green.
- [ ] **Step 6: Report** live Supabase Data API/database-role and bucket-policy smoke checks as not run unless production-equivalent credentials are explicitly available.
