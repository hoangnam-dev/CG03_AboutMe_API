# Backend Release Checklist

## Build and security

- [ ] Release commit and full SHA recorded; reviewed CI run is green.
- [ ] Restore, Release build, full tests, format verification, Docker build, and container smoke passed.
- [ ] Image SHA tag and immutable digest recorded; previous known-good digest retained.
- [ ] Runtime image runs as non-root on port 8080; no secrets or populated `.env` exist in layers/logs.
- [ ] Production Swagger is disabled; frontend origins are exact; HTTPS proxy configuration is verified.

## Configuration and database

- [ ] Platform variables match `.env.example`; required secrets come from the secret store and JWT PFX is mounted read-only.
- [ ] Runtime database role and single-runner migration role are distinct.
- [ ] Migration list/script and `MigrationTests` were reviewed at this SHA.
- [ ] Backup/recovery point, migration operator, target migration, and rollback compatibility decision are recorded.
- [ ] No API instance auto-runs migrations; migration history is verified after the controlled run.

## Deploy and verify

- [ ] Platform health path is `/health`; rollout gate is `/health/ready`.
- [ ] Liveness/readiness, public profile, ProblemDetails, and authentication checks pass.
- [ ] Contact throttling returns 429 when explicitly tested.
- [ ] Supabase Storage health/replace flow passes when the release changes Storage or credentials; fixture cleanup/restore is complete.
- [ ] Logs contain no secret/token/body disclosure and no unexplained error increase.
- [ ] Evidence includes UTC timestamps, operator, image digest, migration history, and redacted smoke output.

## Rollback ownership

- [ ] Rollback owner and observation window are named.
- [ ] Previous image is retained and database compatibility with it is documented.
- [ ] Release acceptance or rollback decision is recorded before the deployment window closes.
