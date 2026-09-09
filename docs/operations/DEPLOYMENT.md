# Deployment Runbook

Deploy an immutable image built from the reviewed commit. The release identifier is the full Git SHA; production must not depend on `latest` for rollback.

## Pre-deploy

1. Complete `RELEASE_CHECKLIST.md`; CI restore/build/test/format/image/smoke gates must be green.
2. Record the image digest and SHA tag, previous known-good digest, operator, window, and rollback owner.
3. Validate production configuration against `ENVIRONMENT.md` without printing secrets.
4. Complete the migration rehearsal. If migrations are pending, execute `MIGRATIONS.md` with one runner before rollout.
5. Confirm the platform routes TLS traffic to container port 8080 and uses `/health` for liveness and `/health/ready` for rollout readiness.

## Rollout

1. Deploy `portfolio-api:<full-git-sha>` by digest where the platform supports it.
2. Keep the previous image available. Do not remove the old release during the observation window.
3. Wait through free-tier cold start. A process start is not success: require HTTP 200 from `/health/ready`.
4. Run the production smoke suite:

```powershell
./scripts/Test-Deployment.ps1 -BaseUri https://api.example.com -FrontendOrigin https://portfolio.example.com -PortfolioSlug portfolio
```

Supply admin credentials via a secure prompt for auth verification. Storage verification replaces the configured profile avatar and is deliberately opt-in:

```powershell
$adminPassword = Read-Host -AsSecureString
./scripts/Test-Deployment.ps1 -BaseUri https://api.example.com -FrontendOrigin https://portfolio.example.com -AdminEmail admin@example.com -AdminPassword $adminPassword -StorageFixturePath ./approved-smoke-avatar.png -AllowStorageReplacement
```

Use an approved reversible fixture and restore the prior avatar after evidence is captured. `-VerifyContactRateLimit` uses invalid requests and does not persist contact records.

## Acceptance

Accept the release only when liveness/readiness are 200, public content is available, invalid input returns safe ProblemDetails, admin login succeeds when tested, rate limiting returns 429 when requested, Storage upload succeeds when requested, and logs show no secret disclosure or unexpected error spike. Record responses, timestamps, image digest, migration history, and operator; redact tokens/cookies.

Readiness failure, migration failure, repeated 5xx, authentication regression, public disclosure regression, or Storage failure blocks release and invokes `ROLLBACK.md`.
