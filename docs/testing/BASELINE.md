# Sprint 0 Baseline

Captured on 2026-09-05 before production feature implementation.

## Repository state

- Solution: `Portfolio.sln`
- Runtime target: .NET 10 (`net10.0`)
- Projects: API, Application, Infrastructure, UnitTests, IntegrationTests
- Implemented runtime capabilities: API foundation, ProblemDetails, Serilog request logging, health checks, ASP.NET Core Identity administrator login/JWT issuance, `AdminPolicy`, administrator dashboard
- Implemented persistence: initial EF Core migration containing Identity and documented portfolio tables
- Missing runtime capabilities: Profile/About/Skills/Experience/Project/Certificate/Resume/Contact feature APIs, Supabase Storage adapter, rate limiting, Docker, CI, RLS/access-policy artifacts
- Bug lessons applicable to Sprint 0: none; `docs/BUG_LESSONS.md` contains no entries

## Verification evidence

```text
dotnet restore
PASS — all projects up to date

dotnet build --configuration Release --no-restore
PASS — 0 warnings, 0 errors

dotnet test --configuration Release --no-build
PASS — 15 total, 15 succeeded, 0 failed, 0 skipped

dotnet format --verify-no-changes --no-restore
FAIL — existing source/test files use LF while `.editorconfig` requires CRLF; Sprint 0 does not reformat unrelated production/test code
```

The existing “integration” project does not yet execute a PostgreSQL container. Its persistence suite inspects EF model metadata, while API tests exercise in-process HTTP behavior. Sprint 1 must add real PostgreSQL migration, constraint, transaction, and query evidence.

## Not verified in Sprint 0

- Docker image/build: no Dockerfile exists.
- Live PostgreSQL/Supabase migration: documentation-only sprint; no environment credentials are required.
- Supabase Storage: no adapter or bucket policy exists.
- GitHub Actions: no workflow exists.
- `dotnet ef migrations list`: the local tool manifest was present but `dotnet-ef` had not been restored during the planning pass; the migration and snapshot were inspected directly.
- Repository-wide formatting: the existing LF/CRLF mismatch must be handled as a deliberate mechanical change before making format verification a blocking CI gate.

These omissions are planned work, not passing evidence.
