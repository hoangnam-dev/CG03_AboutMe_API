# DigitalOcean Continuous Deployment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Automatically deploy the immutable GHCR image produced by a successful `main` workflow run to the DigitalOcean production Droplet, with candidate health checks and automatic container rollback.

**Architecture:** Extend `backend-ci.yml` with a least-privilege `deploy` job that runs only after `publish`, rejects pushes containing EF Core migration changes, and connects with a dedicated SSH key to a root-owned deployment entrypoint on the Droplet. The entrypoint pulls `ghcr.io/hoangnam-dev/cg03-aboutme-api:<full-git-sha>`, validates it on loopback port 8081, switches the fixed production container on port 8080, verifies local and public health, and restores the previous container automatically if rollout verification fails.

**Tech Stack:** GitHub Actions, OpenSSH, Bash, Docker Engine, GHCR, ASP.NET Core health endpoints, xUnit deployment-asset tests.

**Spec:** `docs/BACKEND_SPEC.md` sections 19-23, `docs/operations/DEPLOYMENT.md`, `docs/operations/MIGRATIONS.md`, and `docs/operations/ROLLBACK.md`.

## Global Constraints

- Deploy only the SHA-tagged image already verified and published by the `publish` job; never rebuild on the Droplet and never deploy `latest`.
- Keep `/opt/cg03aboutme-be/shared/app.env` and the JWT PFX exclusively on the Droplet; GitHub must not receive application or database secrets.
- Use a dedicated SSH key restricted to the root-owned deployment entrypoint; do not reuse the operator's personal SSH key.
- Pin the Droplet host key with `StrictHostKeyChecking=yes`; do not run `ssh-keyscan` inside the workflow.
- Reserve container name `cg03aboutme-api-candidate` and loopback port 8081 for deployment validation.
- Preserve exactly the immediate previous production container as `cg03aboutme-api-previous` for binary rollback.
- Do not run EF Core migrations from the application container or automatic deploy job. A push that changes `src/Portfolio.Infrastructure/Persistence/Migrations/` must publish its image but fail closed before SSH deployment.
- Successful rollout requires HTTP 200 from `/health`, `/health/ready`, and `/health/supabase` locally, plus HTTP 200 from the public `/health/ready` endpoint.
- Nginx remains pointed at `127.0.0.1:8080`; CD must not modify or reload Nginx.

---

### Task 1: Define executable CD contracts

**Files:**
- Modify: `tests/Portfolio.IntegrationTests/Deployment/DeploymentAssetTests.cs`
- Create: `scripts/deploy-production.sh`
- Modify: `.github/workflows/backend-ci.yml`

**Interfaces:**
- Consumes: the published tag `ghcr.io/hoangnam-dev/cg03-aboutme-api:${GITHUB_SHA}` and `SSH_ORIGINAL_COMMAND` in the exact form `deploy <40-lowercase-hex-sha>`.
- Produces: a workflow `deploy` job and a Bash deployment entrypoint whose required safety properties are executable test contracts.

- [ ] **Step 1: Add failing deployment-script asset tests**

Add tests that read `scripts/deploy-production.sh` and assert it contains all of the following exact contracts:

```csharp
[Fact]
public void ProductionDeployScriptUsesImmutableCandidateAndRollbackGates()
{
    var script = Read("scripts/deploy-production.sh");

    Assert.Contains("set -Eeuo pipefail", script, StringComparison.Ordinal);
    Assert.Contains("ghcr.io/hoangnam-dev/cg03-aboutme-api:${release_sha}", script, StringComparison.Ordinal);
    Assert.Contains("127.0.0.1:8081:8080", script, StringComparison.Ordinal);
    Assert.Contains("127.0.0.1:8080:8080", script, StringComparison.Ordinal);
    Assert.Contains("/health/ready", script, StringComparison.Ordinal);
    Assert.Contains("/health/supabase", script, StringComparison.Ordinal);
    Assert.Contains("cg03aboutme-api-previous", script, StringComparison.Ordinal);
    Assert.Contains("rollback", script, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("deployed-sha", script, StringComparison.Ordinal);
    Assert.DoesNotContain("latest", script, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("docker system prune", script, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Add failing workflow contract tests**

Add a test that isolates the `deploy` job from `.github/workflows/backend-ci.yml` and asserts:

```csharp
Assert.Contains("needs: publish", deployJob, StringComparison.Ordinal);
Assert.Contains("environment: production", deployJob, StringComparison.Ordinal);
Assert.Contains("src/Portfolio.Infrastructure/Persistence/Migrations/", deployJob, StringComparison.Ordinal);
Assert.Contains("StrictHostKeyChecking=yes", deployJob, StringComparison.Ordinal);
Assert.Contains("secrets.PRODUCTION_SSH_PRIVATE_KEY", deployJob, StringComparison.Ordinal);
Assert.Contains("secrets.PRODUCTION_SSH_KNOWN_HOSTS", deployJob, StringComparison.Ordinal);
Assert.Contains("vars.PRODUCTION_SSH_HOST", deployJob, StringComparison.Ordinal);
Assert.Contains("vars.PRODUCTION_SSH_USER", deployJob, StringComparison.Ordinal);
Assert.Contains("deploy ${{ github.sha }}", deployJob, StringComparison.Ordinal);
Assert.DoesNotContain("ssh-keyscan", deployJob, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("packages: write", deployJob, StringComparison.Ordinal);
```

- [ ] **Step 3: Run the focused tests and verify RED**

Run:

```powershell
dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~DeploymentAssetTests
```

Expected: failures report the missing deployment script and missing `deploy` workflow job.

---

### Task 2: Implement the fail-safe Droplet deployment entrypoint

**Files:**
- Create: `scripts/deploy-production.sh`
- Test: `tests/Portfolio.IntegrationTests/Deployment/DeploymentAssetTests.cs`

**Interfaces:**
- Consumes: `SSH_ORIGINAL_COMMAND="deploy <sha>"`, Docker network `cg03aboutme-prod`, `/opt/cg03aboutme-be/shared/app.env`, and `/opt/cg03aboutme-be/shared/certificates/jwt-signing-2026-09.pfx`.
- Produces: running container `cg03aboutme-api`, stopped rollback container `cg03aboutme-api-previous`, and `/opt/cg03aboutme-be/shared/deployed-sha` containing the accepted SHA.

- [ ] **Step 1: Validate the forced SSH command and prerequisites**

Implement Bash with `set -Eeuo pipefail`. Parse only this anchored form:

```bash
if [[ ${SSH_ORIGINAL_COMMAND:-} =~ ^deploy\ ([0-9a-f]{40})$ ]]; then
  release_sha=${BASH_REMATCH[1]}
else
  echo "Rejected deployment command." >&2
  exit 64
fi
```

Use these fixed values and fail before touching production if any prerequisite is absent:

```bash
image="ghcr.io/hoangnam-dev/cg03-aboutme-api:${release_sha}"
production_name="cg03aboutme-api"
candidate_name="cg03aboutme-api-candidate"
previous_name="cg03aboutme-api-previous"
network_name="cg03aboutme-prod"
env_file="/opt/cg03aboutme-be/shared/app.env"
pfx_file="/opt/cg03aboutme-be/shared/certificates/jwt-signing-2026-09.pfx"
deployed_sha_file="/opt/cg03aboutme-be/shared/deployed-sha"
```

Require `docker`, `curl`, the Docker network, readable env/PFX files, an existing running production container, and a free loopback port 8081.

- [ ] **Step 2: Pull and validate a candidate without changing production**

Pull the SHA tag, remove only a stale container with the exact reserved candidate name, and start the candidate with the same production settings currently proven on the Droplet:

```bash
docker run --detach \
  --name "$candidate_name" \
  --network "$network_name" \
  --env-file "$env_file" \
  --env ASPNETCORE_ENVIRONMENT=Production \
  --env 'ASPNETCORE_URLS=http://+:8080' \
  --mount "type=bind,src=${pfx_file},dst=/run/secrets/jwt-signing.pfx,readonly" \
  --publish 127.0.0.1:8081:8080 \
  --security-opt no-new-privileges:true \
  "$image"
```

Poll `/health`, `/health/ready`, and `/health/supabase` through `http://127.0.0.1:8081` with `X-Forwarded-Proto: https`, at most 60 attempts with a one-second interval. On failure, print the candidate's last 100 log lines, remove only the candidate, and leave production untouched.

- [ ] **Step 3: Cut over with an immediately restorable container**

After candidate acceptance, stop and remove the candidate. Remove only an already-stopped container with exact name `cg03aboutme-api-previous`, stop production, rename it to `cg03aboutme-api-previous`, and start the same accepted image as `cg03aboutme-api` on `127.0.0.1:8080` with `--restart unless-stopped` and otherwise identical settings.

- [ ] **Step 4: Verify and automatically rollback**

Poll the three local health endpoints on port 8080, then require HTTP 200 from `https://api.noveraxiv.com/health/ready`. If container creation or any post-cutover check fails:

1. print the failed container's last 100 log lines;
2. remove only the failed `cg03aboutme-api` container;
3. rename `cg03aboutme-api-previous` back to `cg03aboutme-api`;
4. start it;
5. require local `/health/ready` HTTP 200;
6. exit non-zero so GitHub marks deployment failed.

On success, atomically write the release SHA by writing `${deployed_sha_file}.tmp` and renaming it to `$deployed_sha_file`. Keep the previous container stopped for the observation window.

- [ ] **Step 5: Verify Bash syntax and contract tests**

Run:

```powershell
bash -n scripts/deploy-production.sh
dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~DeploymentAssetTests
```

Expected: Bash syntax is valid and all deployment asset tests pass.

---

### Task 3: Add the GitHub Actions production deploy job

**Files:**
- Modify: `.github/workflows/backend-ci.yml`
- Test: `tests/Portfolio.IntegrationTests/Deployment/DeploymentAssetTests.cs`

**Interfaces:**
- Consumes: successful `publish`, GitHub Environment `production`, variables `PRODUCTION_SSH_HOST`, `PRODUCTION_SSH_PORT`, `PRODUCTION_SSH_USER`, and secrets `PRODUCTION_SSH_PRIVATE_KEY`, `PRODUCTION_SSH_KNOWN_HOSTS`.
- Produces: one forced SSH command, `deploy ${{ github.sha }}`, to the Droplet.

- [ ] **Step 1: Add the main-push-only deploy job**

Add `deploy` with the same main-push condition as `publish`, `needs: publish`, `environment: production`, `timeout-minutes: 10`, `contents: read`, and production-scoped concurrency with `cancel-in-progress: false`. Check out with `fetch-depth: 0`.

- [ ] **Step 2: Add the automatic migration stop gate**

For push events, compare `${{ github.event.before }}` with `${{ github.sha }}` and fail before SSH when this path changed:

```text
src/Portfolio.Infrastructure/Persistence/Migrations/
```

The error must direct the operator to `docs/operations/MIGRATIONS.md` and manual controlled deployment. Publication remains successful so the reviewed SHA image is available after migration approval.

- [ ] **Step 3: Configure pinned-key SSH without third-party deploy actions**

Create `~/.ssh/id_ed25519` and `~/.ssh/known_hosts` under `umask 077` using the two production secrets. Invoke the native client with:

```bash
ssh \
  -i ~/.ssh/id_ed25519 \
  -o BatchMode=yes \
  -o IdentitiesOnly=yes \
  -o StrictHostKeyChecking=yes \
  -p "${{ vars.PRODUCTION_SSH_PORT }}" \
  "${{ vars.PRODUCTION_SSH_USER }}@${{ vars.PRODUCTION_SSH_HOST }}" \
  "deploy ${{ github.sha }}"
```

Do not echo secrets, print private-key fingerprints, or disable host verification.

- [ ] **Step 4: Run focused and full verification**

Run:

```powershell
dotnet format Portfolio.sln --verify-no-changes --no-restore
dotnet build Portfolio.sln --configuration Release --no-restore
dotnet test Portfolio.sln --configuration Release --no-build
```

Expected: formatting and build succeed with zero errors, and all tests pass.

---

### Task 4: Provision the restricted CD identity and production environment

**Files:**
- Modify: `docs/operations/DEPLOYMENT.md`
- Modify: `docs/operations/ROLLBACK.md`
- Modify: `docs/operations/RELEASE_CHECKLIST.md`
- Modify: `docs/operations/DOCKER_SETUP_DEPLOYMENT_GUIDE.md`

**Interfaces:**
- Consumes: reviewed `scripts/deploy-production.sh` and a newly generated Ed25519 key pair.
- Produces: `/opt/cg03aboutme-be/bin/deploy-production.sh`, a forced-command authorized key for user `deploy`, and configured GitHub Environment `production`.

- [ ] **Step 1: Install the root-owned entrypoint on the Droplet**

Document copying the reviewed script to a temporary path, validating `bash -n`, then installing it as:

```bash
sudo install -d -m 755 -o root -g root /opt/cg03aboutme-be/bin
sudo install -m 755 -o root -g root deploy-production.sh /opt/cg03aboutme-be/bin/deploy-production.sh
```

Record its SHA-256 checksum and initialize the current accepted release marker to `96672eaf4ebc6826fbce749ab36bec8ec0273af4`.

- [ ] **Step 2: Create and restrict a dedicated Actions SSH key**

Document generating a new Ed25519 key locally, storing only its public half on the server, and prefixing its `authorized_keys` line with:

```text
restrict,command="/opt/cg03aboutme-be/bin/deploy-production.sh"
```

Verify that `ssh ... "deploy 96672eaf4ebc6826fbce749ab36bec8ec0273af4"` invokes the entrypoint and that `ssh ... "bash"` is rejected by the script. Do not replace or expose the operator key.

- [ ] **Step 3: Pin the Droplet host identity**

Document obtaining the ED25519 host-key fingerprint directly on the Droplet with `sudo ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub`, comparing it to the client-observed fingerprint, and only then saving the verified known-hosts line. Do not treat an unverified `ssh-keyscan` result as identity proof.

- [ ] **Step 4: Configure the GitHub production environment**

Create GitHub Environment `production` with:

```text
Variables:
PRODUCTION_SSH_HOST=152.42.229.122
PRODUCTION_SSH_PORT=22
PRODUCTION_SSH_USER=deploy

Secrets:
PRODUCTION_SSH_PRIVATE_KEY=<dedicated private key>
PRODUCTION_SSH_KNOWN_HOSTS=<verified known_hosts line>
```

Do not add `app.env`, JWT certificate/password, PostgreSQL credentials, or Supabase service-role key to GitHub.

- [ ] **Step 5: Document operational behavior**

Update the runbooks to state that ordinary non-migration pushes deploy automatically, migration pushes stop after image publication, `cg03aboutme-api-previous` is the rollback target, and container/image cleanup occurs only after an observation window and an explicit operator decision.

---

### Task 5: Prove CD with a no-op production release

**Files:**
- Modify: `docs/operations/RELEASE_CHECKLIST.md`

**Interfaces:**
- Consumes: merged workflow, installed forced-command entrypoint, configured production environment, and a main push with no migration changes.
- Produces: recorded evidence that the exact Git SHA reached production and rollback remains available.

- [ ] **Step 1: Trigger the first eligible main workflow**

Merge a documentation-only change outside the migrations directory. Confirm `verify`, `publish`, and `deploy` run sequentially and all finish green.

- [ ] **Step 2: Verify the deployed identity on the Droplet**

Run:

```bash
cat /opt/cg03aboutme-be/shared/deployed-sha
docker inspect cg03aboutme-api --format 'ImageRef={{.Config.Image}} ImageId={{.Image}} Restart={{.HostConfig.RestartPolicy.Name}}'
docker ps -a --filter 'name=^/cg03aboutme-api-previous$' --format 'table {{.Names}}\t{{.Status}}\t{{.Image}}'
```

Expected: `deployed-sha` and `ImageRef` contain the workflow SHA, production is `unless-stopped`, and the immediately previous container is retained but stopped.

- [ ] **Step 3: Verify production acceptance**

Run all three public health checks, exercise one public portfolio request, inspect API and Nginx logs for unexpected 5xx responses, and run `scripts/Test-Deployment.ps1` from an operator workstation without the destructive Storage option.

- [ ] **Step 4: Prove the migration gate separately**

Validate the workflow migration-diff shell logic against a known commit range containing a migration and confirm it exits non-zero before the SSH step. Do not create or deploy a fake production migration solely for this test.

- [ ] **Step 5: Record the acceptance evidence**

Record UTC time, workflow URL, deployed SHA, GHCR repository digest, three health statuses, retained rollback image, and redacted log result in the release checklist. Do not record credentials, tokens, connection strings, or uploaded file content.

---

## Self-Review

- **Spec coverage:** immutable artifact deployment, least privilege, externalized secrets, controlled migrations, health-gated rollout, rollback, and production evidence are covered.
- **Placeholder scan:** all paths, container names, ports, image names, health routes, variables, and secrets are explicit; secret values are intentionally supplied only through GitHub Environment secrets.
- **Interface consistency:** the workflow emits `deploy <40-hex-sha>`, the forced entrypoint accepts exactly that command, and both use the same SHA-tagged GHCR image.
