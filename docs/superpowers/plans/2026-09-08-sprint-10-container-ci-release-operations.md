# Sprint 10 Container, CI, Release, and Operations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a reproducible non-root .NET 10 container, least-privilege CI verification, safe migration/deployment operations, and repeatable smoke checks for the CG03 AboutMe backend.

**Architecture:** Build the existing `Portfolio.Api` project in a multi-stage image and run the published output as the base image's non-root user on port 8080. Keep schema migration as a separately owned release action, make PR CI deterministic, and provide an operator smoke script for environment-dependent production and Supabase flows.

**Tech Stack:** .NET 10, ASP.NET Core 10, Docker, GitHub Actions, PowerShell, xUnit, EF Core 10, PostgreSQL 17.

**Spec:** `docs/superpowers/plans/2026-09-05-personal-portfolio-backend-roadmap.md` (Sprint 10)

## Global Constraints

- Use .NET SDK/runtime `10.0` and listen on container port `8080`.
- Run the runtime container as the official image's non-root user.
- Do not put secrets in source, image layers, or CI logs.
- Do not run EF migrations automatically from each API instance.
- CI uses least permissions and immutable commit-SHA image tags.
- Production Swagger remains disabled unless separately approved.
- Production migration credentials belong to exactly one controlled runner.

---

### Task 1: Deployment Contract Tests and Migration Rehearsal

**Files:**
- Create: `tests/Portfolio.IntegrationTests/Deployment/DeploymentAssetTests.cs`
- Modify: `tests/Portfolio.IntegrationTests/Persistence/MigrationTests.cs`

**Interfaces:**
- Consumes: repository-root deployment files and `IMigrator`.
- Produces: automated checks for container/CI safety and a successful representative-row migration path.

- [ ] **Step 1: Write failing tests** that require a multi-stage non-root `Dockerfile`, `.dockerignore`, least-privilege CI workflow, smoke scripts, runbooks, and an initial-migration row that survives upgrade to the latest migration.
- [ ] **Step 2: Run the focused tests** with `dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~DeploymentAssetTests|FullyQualifiedName~RepresentativeRowsSurviveUpgrade"` and confirm failures are caused by missing Sprint 10 assets.
- [ ] **Step 3: Implement Tasks 2–4**, then rerun the focused tests and confirm they pass.

### Task 2: Non-root Container and CI

**Files:**
- Create: `Dockerfile`
- Create: `.dockerignore`
- Create: `.github/workflows/backend-ci.yml`

**Interfaces:**
- Consumes: `Portfolio.sln`, `src/Portfolio.Api/Portfolio.Api.csproj`, `/health`.
- Produces: image `portfolio-api:<commit-sha>` running `Portfolio.Api.dll` on `http://+:8080` as `$APP_UID`; CI restore/build/test/format/image/smoke gates.

- [ ] **Step 1: Add the Docker build** using `mcr.microsoft.com/dotnet/sdk:10.0` and `mcr.microsoft.com/dotnet/aspnet:10.0`, restore before publish, copy only published output to runtime, set `ASPNETCORE_HTTP_PORTS=8080`, `EXPOSE 8080`, and `USER $APP_UID`.
- [ ] **Step 2: Limit build context** by excluding Git metadata, secrets, environment files, build output, IDE state, tests results, and documentation not needed by publish.
- [ ] **Step 3: Add CI** with `permissions: contents: read`, `actions/checkout@v4`, `actions/setup-dotnet@v4`, .NET `10.0.x`, restore, Release build/test, `dotnet format --verify-no-changes`, SHA-tagged Docker build, and container smoke execution.

### Task 3: Environment Contract and Smoke Automation

**Files:**
- Modify: `.env.example`
- Create: `scripts/Test-Container.ps1`
- Create: `scripts/Test-Deployment.ps1`

**Interfaces:**
- Consumes: built Docker image or deployed HTTPS base URI, optional admin credentials and Storage fixture.
- Produces: local image smoke verification and post-deploy checks for health, readiness, safe ProblemDetails, auth, public API, contact throttling, and opt-in Storage upload/delete.

- [ ] **Step 1: Complete `.env.example`** with safe placeholders for every currently bound `ConnectionStrings`, `Frontend`, `Jwt`, `RefreshToken`, `BootstrapAdmin`, `SupabaseStorage`, `Upload`, `SkillIcons`, and rate-limit setting.
- [ ] **Step 2: Add local container smoke** that starts a uniquely named container in Development, verifies liveness, readiness failure without dependencies, safe ProblemDetails, a public API response, auth response, contact throttling, and always removes its exact container.
- [ ] **Step 3: Add deployed smoke** that requires a base URI, verifies `/health` and `/health/ready`, checks a public endpoint, ProblemDetails, login when credentials are supplied, rate limiting when explicitly enabled, and a Storage upload/delete flow when credentials plus a fixture file are supplied.

### Task 4: Operational Documentation

**Files:**
- Create: `README.md`
- Create: `docs/operations/ENVIRONMENT.md`
- Create: `docs/operations/MIGRATIONS.md`
- Create: `docs/operations/DEPLOYMENT.md`
- Create: `docs/operations/ROLLBACK.md`
- Create: `docs/operations/RELEASE_CHECKLIST.md`
- Modify: `docs/LOCAL_SETUP.md`

**Interfaces:**
- Consumes: Docker/CI/smoke commands from Tasks 2–3 and the existing EF migration tool manifest.
- Produces: operator procedures with single-runner ownership, backup gates, clean/upgrade rehearsal, immutable release identification, health criteria, rollback compatibility checks, and production verification evidence.

- [ ] **Step 1: Document build and configuration** including secret-store requirements, mounted JWT certificate, port 8080, and no automatic migrations.
- [ ] **Step 2: Document migration rehearsal and execution** using a fresh Release build, migration listing/script review, clean and representative-data tests, backup, controlled `dotnet ef database update`, and history verification.
- [ ] **Step 3: Document deployment and rollback** using the commit SHA, readiness gate, previous-image retention, database compatibility decision, and explicit rollback criteria.
- [ ] **Step 4: Add release checklist** covering CI, security, migrations, image identity, environment, critical smoke flows, evidence, and rollback ownership.

### Task 5: Full Verification

**Files:**
- Modify only files needed to correct verification failures introduced by Sprint 10.

**Interfaces:**
- Consumes: all Sprint 10 deliverables.
- Produces: fresh evidence for the Definition of Done.

- [ ] **Step 1: Run** `dotnet restore Portfolio.sln`.
- [ ] **Step 2: Run** `dotnet build Portfolio.sln --configuration Release --no-restore`.
- [ ] **Step 3: Run** `dotnet test Portfolio.sln --configuration Release --no-build`.
- [ ] **Step 4: Run** `dotnet format Portfolio.sln --verify-no-changes --no-restore`.
- [ ] **Step 5: Run** `dotnet ef migrations list --project src/Portfolio.Infrastructure --startup-project src/Portfolio.Api --configuration Release --no-build` only after the matching Release build, per BUG-2026-002.
- [ ] **Step 6: Run** `docker build --tag portfolio-api:sprint-10 .` and `scripts/Test-Container.ps1 -Image portfolio-api:sprint-10`.
- [ ] **Step 7: Record any production-only checks as unverified until an authorized target and credentials are provided.**

## Self-Review

- Spec coverage: container, CI, environment contract, controlled migrations, clean/upgrade rehearsal, smoke automation, deployment, rollback, and release evidence are assigned above; actual production deployment is intentionally external to repository implementation.
- Placeholder scan: operational placeholders are explicitly safe environment-template values, not missing implementation steps.
- Type consistency: both scripts accept an image or `System.Uri` target; migration tests use the existing `IMigrator` and pinned migration identifiers.
