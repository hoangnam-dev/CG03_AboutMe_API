---
name: cg03-deployment
description: >
  Design, implement, troubleshoot, or review CI/CD and deployment for the
  CG03 AboutMe ASP.NET Core backend. Use for Docker, GitHub Actions,
  environment variables, secrets, health checks, deployment servers,
  reverse proxy, release directories, migrations during deployment,
  rollback, and production verification.
---

# CG03 Backend Deployment

## Context

Before deployment changes:

1. Read `AGENTS.md`.
2. Read deployment-related sections of `BACKEND_SPEC.md`.
3. Inspect the existing Dockerfile.
4. Inspect `.github/workflows`.
5. Inspect current deployment configuration.

Do not invent infrastructure that the project does not currently use.

## Principles

- Build once and deploy a verified artifact/image.
- Never commit production secrets.
- Prefer immutable versioned releases.
- Keep rollback possible.
- Run build and tests before deployment.
- Use health checks.
- Do not rely on container-local storage for persistent uploads.
- Treat database migrations separately from application startup when
  deployment concurrency can cause risk.

## CI expectations

At minimum, CI should verify as appropriate:

```bash
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

For containerized deployment, also verify:

```bash
docker build .
```

Use immutable image or release identifiers such as commit SHA.

Do not rely only on `latest` for production rollback.

## Secrets and configuration

Keep secrets outside source control.

Typical production secrets/configuration may include:
- PostgreSQL connection string;
- Supabase URL;
- Supabase service credentials where required;
- JWT/authentication configuration;
- frontend origin;
- storage configuration.

Do not print secrets in CI logs.

Use environment-specific configuration and validate required configuration at
application startup.

## Database migrations

Do not blindly run migrations from every application instance at startup.

For production deployments:
- inspect generated migrations;
- understand backward compatibility;
- take backups before destructive changes when appropriate;
- use a controlled migration step;
- keep a recovery or rollback plan for significant schema changes.

Application rollback does not automatically roll back database schema.

## Health checks

Expose health endpoints appropriate for the deployment environment.

Deployment verification should check the real application health endpoint
instead of assuming a successful process start means the application is healthy.

## Reverse proxy and HTTPS

When deploying behind Nginx or another reverse proxy:
- configure forwarded headers correctly;
- terminate HTTPS intentionally;
- restrict exposed application ports;
- ensure proxy timeouts match application behavior;
- verify CORS against the real frontend origin.

## Rollback

Prefer releases that can be switched back without rebuilding.

For release-directory deployments, preserve previous known-good releases.

For container deployment, retain immutable image tags.

Before rollback, check whether the current release introduced a database
migration that prevents an older application version from running safely.

## Troubleshooting

For unexpected deployment/runtime failures, combine this skill with
`systematic-debugging`.

Collect evidence before changing configuration:
- CI logs;
- application logs;
- container/process status;
- health endpoint;
- listening ports;
- reverse-proxy logs;
- environment configuration;
- database connectivity.

Avoid changing multiple infrastructure variables at once without evidence.

## Verification

Deployment-related changes should verify as applicable:

```bash
dotnet build --configuration Release
dotnet test --configuration Release
docker build .
```

Do not claim deployment success without checking the actual deployment or
health endpoint when such access is available.

Report:
- artifact/image version deployed;
- migration status;
- health-check result;
- relevant verification commands;
- any unverified assumptions.
