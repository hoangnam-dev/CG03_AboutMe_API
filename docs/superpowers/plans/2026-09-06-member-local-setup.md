# Member Local Setup Automation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give a new backend member one safe PowerShell entry point for local environment creation, configuration validation, dependency restore, build, optional User Secrets synchronization, and explicitly opted-in Supabase migration.

**Architecture:** Keep onboarding automation in `scripts/Setup-Local.ps1`; the application remains unaware of `.env`. The script creates or reads an ignored `.env`, exports values into its process for `dotnet`/EF commands, validates the same important bounds as startup, and never prints secret values. A repository-local tool manifest pins `dotnet-ef` to the EF Core version used by the solution.

**Tech Stack:** PowerShell 5.1+, .NET SDK 10.0.400, dotnet-ef 10.0.11, ASP.NET Core User Secrets, Supabase PostgreSQL and Storage.

**Spec:** `docs/BACKEND_SPEC.md`, `docs/security/THREAT_MODEL.md`, `docs/STORAGE.md`, `docs/LOCAL_SETUP.md`

## Global Constraints

- Never commit `.env`, database passwords, JWT signing keys, or Supabase server credentials.
- Never print secret values in setup output or test failures.
- Do not modify `appsettings.json` or `launchSettings.json` with secrets.
- Do not apply migrations unless `-ApplyMigrations` is explicitly supplied.
- Use the existing `ConnectionStrings__PostgreSql` design-time contract.
- Support both direct and session-pooler Npgsql connection strings copied from Supabase.
- Keep Storage bucket names exact and distinct.

---

### Task 1: Pin the local EF CLI

**Files:**
- Create: `.config/dotnet-tools.json`

**Interfaces:**
- Consumes: .NET SDK selected by `global.json`.
- Produces: repository-local `dotnet-ef` command version `10.0.11`.

- [x] **Step 1: Add the manifest**

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-ef": {
      "version": "10.0.11",
      "commands": ["dotnet-ef"],
      "rollForward": false
    }
  }
}
```

- [x] **Step 2: Verify manifest discovery**

```powershell
dotnet tool restore
dotnet ef --version
```

Expected: EF CLI version `10.0.11`.

### Task 2: Define setup behavior test-first

**Files:**
- Create: `scripts/tests/Setup-Local.Tests.ps1`
- Create: `scripts/Setup-Local.ps1`

**Interfaces:**
- Consumes: `.env` keys from `.env.example`.
- Produces: process-level environment variables plus optional User Secrets, restore/build, migration-list, and database update operations.

- [x] **Step 1: Write a failing PowerShell harness**

The harness creates ignored temporary `.env` fixtures under `TestResults`, invokes setup with `-ValidateOnly -NonInteractive`, and asserts:

```powershell
Assert-Equal 0 $valid.ExitCode "valid configuration exits successfully"
Assert-Equal 1 $missingJwt.ExitCode "missing JWT signing key fails"
Assert-NotContains $valid.Output "database-secret" "database password is not logged"
Assert-NotContains $valid.Output "storage-secret" "Storage key is not logged"
```

- [x] **Step 2: Run the harness and confirm RED**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/Setup-Local.Tests.ps1
```

Expected: failure because `scripts/Setup-Local.ps1` does not exist.

- [x] **Step 3: Implement the setup script**

The script must provide these switches:

```powershell
param(
    [string]$EnvironmentFile = ".env",
    [switch]$NonInteractive,
    [switch]$ValidateOnly,
    [switch]$SyncUserSecrets,
    [switch]$ApplyMigrations
)
```

When `.env` is absent in interactive mode, prompt for database host/user/password, frontend origin, Supabase URL, and legacy server key; generate a random 48-byte Base64 JWT signing key; and write the canonical configuration with UTF-8 no BOM. Existing files are never overwritten. Validation rejects placeholders, partial Storage configuration, invalid origins/URLs, short JWT keys, unsafe bucket names, duplicate buckets, and out-of-range transport limits.

- [x] **Step 4: Run the harness and confirm GREEN**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/Setup-Local.Tests.ps1
```

Expected: all setup script checks pass without printing fixture secrets.

### Task 3: Expand member onboarding documentation

**Files:**
- Modify: `docs/LOCAL_SETUP.md`

**Interfaces:**
- Produces: clone-to-running-API instructions for environment, Supabase PostgreSQL, Storage buckets, JWT, migrations, Visual Studio/IIS Express, and health checks.

- [x] **Step 1: Document the automated happy path**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Setup-Local.ps1 -SyncUserSecrets
```

Explain that `.env` alone is inert, the script exports it for child commands, and User Secrets are needed for IDE-launched IIS Express unless the IDE supplies environment variables itself.

- [x] **Step 2: Document controlled migration and verification**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Setup-Local.ps1 -SyncUserSecrets -ApplyMigrations
curl.exe -k -i https://localhost:44313/health/supabase
```

Include fresh/existing database prechecks, exact bucket visibility, safe credential handling, and common connection failures.

### Task 4: Verify the deliverable

**Files:**
- Review: all files above.

**Interfaces:**
- Produces: executable onboarding evidence.

- [x] **Step 1: Run script tests**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/Setup-Local.Tests.ps1
```

- [x] **Step 2: Validate the current `.env` without printing values**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Setup-Local.ps1 -ValidateOnly -NonInteractive
```

- [x] **Step 3: Build the solution**

```powershell
dotnet build --configuration Release --no-restore
```

- [x] **Step 4: Review scope and secrets**

Confirm Git ignores `.env`, no tracked file contains credential values, migrations remain opt-in, and no existing user changes were overwritten.
