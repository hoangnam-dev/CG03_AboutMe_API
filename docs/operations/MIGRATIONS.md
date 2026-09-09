# Database Migration Runbook

One named release operator owns each migration execution. API instances must not run `database update` at startup; this prevents two scaled instances from racing and keeps elevated migration credentials out of the runtime container.

## Rehearsal gate

From the exact release commit:

```powershell
dotnet restore Portfolio.sln
dotnet build Portfolio.sln --configuration Release --no-restore
dotnet tool restore
dotnet ef migrations list --project src/Portfolio.Infrastructure --startup-project src/Portfolio.Api --configuration Release --no-build
dotnet test tests/Portfolio.IntegrationTests/Portfolio.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~MigrationTests
```

The migration suite applies every migration to clean PostgreSQL, upgrades representative rows from `InitialCreate`, and verifies that unsafe legacy certificate nulls block the correction migration. Per BUG-2026-002, use `--no-build` only after the matching Release build; inspect the latest migration name and generated `Up`/`Down` before any add/remove/update command.

Generate and review an idempotent forward script when a schema change exists:

```powershell
dotnet ef migrations script --idempotent --project src/Portfolio.Infrastructure --startup-project src/Portfolio.Api --configuration Release --no-build --output TestResults/migration.sql
```

Review locks, data rewrites, defaults, destructive statements, required extensions, and compatibility with both old and new application versions.

## Production execution

1. Record release SHA, current migration history, operator, UTC start time, and target database.
2. Confirm the tested backup/point-in-time recovery mechanism and record its recovery point. A destructive or data-rewriting migration cannot proceed without a restorable backup.
3. Stop the release if application tables exist without a trustworthy `__EFMigrationsHistory`.
4. Pause rollout; keep existing compatible API instances running only when the reviewed migration permits it.
5. Give the migration credential to the single runner, not the API service.
6. Apply the reviewed target explicitly:

```powershell
dotnet ef database update <target-migration> --project src/Portfolio.Infrastructure --startup-project src/Portfolio.Api --configuration Release --no-build --connection "<migration-connection-from-secret-store>"
```

7. Re-query `public."__EFMigrationsHistory"`, verify expected constraints/indexes, revoke runner access, and record UTC completion.
8. Continue deployment only when `/health/ready` and the critical data checks pass.

## Failure decision

Do not repeatedly rerun a failed migration. Capture the database error without credentials, stop rollout, and choose either a reviewed forward repair or restore. An EF `Down` operation is not assumed safe: consult `ROLLBACK.md` and assess data loss and old-binary compatibility first.
