# Frontend Origin Allowlist Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the single trusted frontend origin with an exact-match allowlist configurable through JSON or indexed environment variables.

**Architecture:** `FrontendOptions` exposes `Origins`. Startup validation rejects empty, malformed, path-bearing, or duplicate entries. CORS and auth Origin validation consume the same validated allowlist so browser policy and CSRF defense cannot drift.

**Tech Stack:** .NET 10, ASP.NET Core 10, options validation, CORS, xUnit integration tests

**Spec:** `docs/security/THREAT_MODEL.md`, `docs/api/API_CONTRACT.md`, `docs/security/AUTHENTICATION_GUIDE.md`

## Global Constraints

- Match origins exactly and never combine wildcard origins with credentialed CORS.
- Preserve case-insensitive auth route matching required by `BUG-2026-003`.
- Production configuration must contain only explicitly trusted origins.
- Adding configuration requires an application restart because options are bound at startup.

---

### Task 1: Specify allowlist validation and request behavior

**Files:**
- Modify: `tests/Portfolio.IntegrationTests/Authentication/AuthenticationOptionsValidatorTests.cs`
- Modify: `tests/Portfolio.IntegrationTests/Api/AuthApiTests.cs`
- Modify: `tests/Portfolio.IntegrationTests/Api/ApiFoundationTests.cs`

**Interfaces:**
- Consumes: existing `FrontendOptionsValidator` and `AuthOriginValidationMiddleware`
- Produces: failing tests for multiple trusted origins and invalid allowlists

- [x] **Step 1: Write failing validator tests**

Add tests that require at least one origin and reject malformed, path-bearing, and duplicate origins.

- [x] **Step 2: Write a failing API test**

Configure two trusted origins and prove that login accepts both while rejecting a third origin.

- [x] **Step 3: Run focused tests and confirm RED**

```powershell
dotnet test tests\Portfolio.IntegrationTests\Portfolio.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~AuthenticationOptionsValidatorTests|FullyQualifiedName~AuthApiTests"
```

Expected: compilation or assertion failure because `FrontendOptions.Origins` does not exist and the middleware accepts only one origin.

---

### Task 2: Implement one validated source of truth

**Files:**
- Modify: `src/Portfolio.Api/Configuration/FrontendOptions.cs`
- Modify: `src/Portfolio.Api/Configuration/FrontendOptionsValidator.cs`
- Modify: `src/Portfolio.Api/Authentication/AuthOriginValidationMiddleware.cs`
- Modify: `src/Portfolio.Api/Extensions/ServiceCollectionExtensions.cs`

**Interfaces:**
- Consumes: `Frontend:Origins` configuration array
- Produces: `string[] FrontendOptions.Origins` shared by CORS and Origin middleware

- [x] **Step 1: Replace the scalar option**

Expose `Origins` as a non-null string array.

- [x] **Step 2: Validate every entry**

Require a non-empty list of absolute HTTP(S) origins without paths, queries, fragments, user info, or trailing slash variants. Repeated entries remain harmless because the middleware materializes the allowlist as a set, and allowing them avoids false startup failures when configuration providers merge array indices.

- [x] **Step 3: Apply the allowlist**

Pass all origins to `WithOrigins` and use ordinal exact membership in `AuthOriginValidationMiddleware`.

- [x] **Step 4: Run focused tests and confirm GREEN**

```powershell
dotnet test tests\Portfolio.IntegrationTests\Portfolio.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~AuthenticationOptionsValidatorTests|FullyQualifiedName~AuthApiTests"
```

Expected: all selected tests pass.

---

### Task 3: Migrate configuration and documentation

**Files:**
- Modify: `src/Portfolio.Api/appsettings.json`
- Modify: `src/Portfolio.Api/appsettings.Development.json`
- Modify: `.env.example`
- Modify: `scripts/Setup-Local.ps1`
- Modify: `scripts/tests/Setup-Local.Tests.ps1`
- Modify: `docs/BACKEND_SPEC.md`
- Modify: `docs/api/API_CONTRACT.md`
- Modify: `docs/LOCAL_SETUP.md`
- Modify: `docs/security/AUTHENTICATION_GUIDE.md`
- Modify: `tests/Portfolio.IntegrationTests/Api/AuthenticationFlowTests.cs`

**Interfaces:**
- Consumes: indexed environment variables such as `Frontend__Origins__0`
- Produces: consistent local, test, and production configuration examples

- [x] **Step 1: Update configuration keys**

Use `Frontend:Origins` in JSON and `Frontend__Origins__N` in environment variables.

- [x] **Step 2: Update local setup output and its test**

Keep the setup prompt singular but write its value to allowlist index zero.

- [x] **Step 3: Update contracts and examples**

Document local Swagger, Kestrel, Next.js, Bruno/Postman, restart semantics, and production allowlist restrictions.

- [ ] **Step 4: Run verification**

```powershell
dotnet build Portfolio.sln --configuration Release
dotnet test Portfolio.sln --configuration Release
```

Expected: build and all tests pass.

Status: Release build passed. The full test command ran 239 tests with 205 succeeded and 34 PostgreSQL fixture errors because the local Docker daemon was unavailable; focused allowlist tests passed separately.

- [ ] **Step 5: Commit when explicitly requested**

```powershell
git add src tests scripts docs .env.example
git commit -m "feat(auth): support trusted frontend origin allowlist"
```
