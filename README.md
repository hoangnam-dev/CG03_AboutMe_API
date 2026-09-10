# CG03 AboutMe Backend

ASP.NET Core 10 REST API for a multilingual personal portfolio. The solution uses layered API/Application/Infrastructure projects, EF Core with PostgreSQL, ASP.NET Core Identity authentication, Supabase Storage, ProblemDetails, Serilog, xUnit, and Docker.

## Local development

Requirements and secure onboarding are documented in [docs/LOCAL_SETUP.md](docs/LOCAL_SETUP.md). The shortest supported setup is:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Setup-Local.ps1 -SyncUserSecrets
dotnet run --project src/Portfolio.Api/Portfolio.Api.csproj
```

Swagger is available only in Development. Liveness is `/health`; dependency readiness is `/health/ready`.

## Verification

```powershell
dotnet restore Portfolio.sln
dotnet build Portfolio.sln --configuration Release --no-restore
dotnet test Portfolio.sln --configuration Release --no-build
dotnet format Portfolio.sln --verify-no-changes --no-restore
docker build --tag portfolio-api:local .
./scripts/Test-Container.ps1 -Image portfolio-api:local
```

PostgreSQL integration tests and the container smoke test require Docker. CI runs every gate above and identifies images by commit SHA.

## Operations

- [Hướng dẫn Docker chi tiết cho member mới (tiếng Việt)](docs/operations/DOCKER_SETUP_DEPLOYMENT_GUIDE.md)
- [Environment reference](docs/operations/ENVIRONMENT.md)
- [Migration runbook](docs/operations/MIGRATIONS.md)
- [Deployment runbook](docs/operations/DEPLOYMENT.md)
- [Rollback guide](docs/operations/ROLLBACK.md)
- [Release checklist](docs/operations/RELEASE_CHECKLIST.md)

The API never runs migrations automatically. Production schema changes are owned by one controlled migration runner before application rollout.
