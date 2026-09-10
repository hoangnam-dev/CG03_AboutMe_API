# Rollback and Recovery Guide

Rollback means switching to a previously verified immutable image. It does not automatically reverse database schema or data.

## Decision gate

Before switching binaries, compare the failed release's migrations with the previous application version:

- If no migration ran, switch traffic to the previous image digest.
- If only backward-compatible additive changes ran, retain the schema and switch after confirming the old image can ignore them.
- If a migration removed/renamed data or changed invariants incompatibly, do not start the old image. Choose a reviewed forward repair or restore the recorded backup/recovery point.
- Never execute `dotnet ef database update <older-migration>` merely because `Down` exists; first review data loss, locks, constraints, and migration history.

## Procedure

1. Stop further rollout and name the incident/rollback owner.
2. Preserve application, proxy, platform, migration, and health evidence without secrets.
3. Disable traffic to unhealthy new instances.
4. Make the database compatibility decision above and record it.
5. Select the previous known-good image by digest/SHA; do not rebuild it.
6. Switch the platform release and verify `/health`, then `/health/ready`.
7. Run `scripts/Test-Deployment.ps1` against the restored release and inspect logs/error rate.
8. Confirm migration history and representative data remain coherent.
9. Close rollback only after the observation window; create a root-cause follow-up before retrying release.

If restoration is required, isolate writes, restore to a new database when possible, validate migration history and critical records, then deliberately repoint the application. Keep the failed release and database evidence until review is complete.
