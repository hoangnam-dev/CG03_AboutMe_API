# GHCR Verified Image Publish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish the exact Docker image that passed CI smoke checks to GHCR under an immutable commit-SHA tag after a push to `main`.

**Architecture:** The existing least-privilege `verify` job builds and smoke-tests the image, then exports it as a one-day workflow artifact only for `main` pushes. A separate `publish` job receives `packages: write`, downloads that verified image without rebuilding, authenticates with `GITHUB_TOKEN`, and pushes `ghcr.io/hoangnam-dev/cg03-aboutme-api:${{ github.sha }}`.

**Tech Stack:** GitHub Actions, Docker, GHCR, xUnit deployment-asset tests.

**Spec:** `docs/BACKEND_SPEC.md` sections 20-21 and `docs/operations/DOCKER_SETUP_DEPLOYMENT_GUIDE.md` section 9.

## Global Constraints

- Pull requests retain workflow-level `contents: read` and never receive package-write permission.
- Only the `publish` job receives `packages: write`, and it runs only for pushes to `refs/heads/main`.
- Publish the image already smoke-tested by `verify`; never rebuild it in `publish`.
- Use only the commit SHA as the production image tag; do not depend on `latest`.
- Do not add registry passwords or personal access tokens; use the job-scoped `GITHUB_TOKEN`.

---

### Task 1: Publish the verified image to GHCR

**Files:**
- Modify: `.github/workflows/backend-ci.yml`
- Modify: `tests/Portfolio.IntegrationTests/Deployment/DeploymentAssetTests.cs`
- Modify: `docs/operations/DOCKER_SETUP_DEPLOYMENT_GUIDE.md`
- Modify: `docs/architecture/CURRENT_PROCESS_FLOWS.md`

**Interfaces:**
- Consumes: local image `portfolio-api:${{ github.sha }}` produced by the `verify` job.
- Produces: immutable registry image `ghcr.io/hoangnam-dev/cg03-aboutme-api:${{ github.sha }}` and a one-day transfer artifact named `portfolio-api-${{ github.sha }}`.

- [x] **Step 1: Write the failing workflow-contract test**

Add `ContinuousDeliveryPublishesVerifiedShaImageOnlyFromMain` to `DeploymentAssetTests`. Normalize CRLF to LF, isolate the `publish` job text, and assert the workflow contains the main-push condition, `needs: verify`, job-level `packages: write`, upload/download artifact actions, `docker save`, `docker load`, GHCR login, and the exact SHA-tagged image. Assert the `publish` job contains no `docker build` command.

- [x] **Step 2: Run the focused test and verify RED**

```powershell
dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~DeploymentAssetTests
```

Expected: the new test fails because the workflow has no artifact transfer or GHCR publish job.

- [x] **Step 3: Export the smoke-tested image from `verify`**

After `Smoke-test container`, add main-push-only steps that run `docker save --output portfolio-api.tar portfolio-api:${{ github.sha }}` and upload that file using `actions/upload-artifact@v7`, name `portfolio-api-${{ github.sha }}`, and `retention-days: 1`.

- [x] **Step 4: Add the least-privilege publish job**

Add `publish` with the exact condition `github.event_name == 'push' && github.ref == 'refs/heads/main'`, `needs: verify`, Ubuntu runner, ten-minute timeout, and job permissions `contents: read`, `packages: write`. Download the artifact with `actions/download-artifact@v8`, load it, authenticate using `docker/login-action@v4` with `github.actor` and `secrets.GITHUB_TOKEN`, tag it as `ghcr.io/hoangnam-dev/cg03-aboutme-api:${{ github.sha }}`, push that tag, and print its repository digest.

- [x] **Step 5: Update release documentation**

Document that `main` CI now publishes the already-verified image, record the exact GHCR namespace, and require Droplet deployments to use the SHA tag or resolved digest. Keep manual publishing instructions as an emergency fallback only.

- [x] **Step 6: Run verification**

```powershell
dotnet format Portfolio.sln --verify-no-changes --no-restore
dotnet build Portfolio.sln --configuration Release --no-restore
dotnet test Portfolio.sln --configuration Release --no-build
```

Expected: formatting and build pass with zero errors, and all tests pass. Actual GHCR publication remains verified by the next `main` workflow run because local execution cannot issue a GitHub job token.

Actual: format verification passed; Release build passed with zero warnings and zero errors; all 383 tests passed; Docker build and container smoke passed. GHCR publication awaits the next `main` workflow run.
