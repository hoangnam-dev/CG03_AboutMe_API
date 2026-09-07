# Access Token + Refresh Token Authentication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the access-token-only administrator login with production-oriented RS256 access tokens, opaque rotating refresh tokens, revocable sessions, browser cookie/CSRF controls, and complete PostgreSQL-backed tests.

**Architecture:** Preserve the current three-project Layered Architecture. `Portfolio.Application` owns authentication use cases and feature-specific persistence/security abstractions; `Portfolio.Infrastructure` implements Identity, RSA JWT, HMAC refresh-token protection and PostgreSQL transactions; `Portfolio.Api` owns cookies, Origin enforcement, CORS, rate limiting and HTTP contracts. No Supabase Auth, generic repository, custom global Unit of Work, or new Domain project is introduced.

**Tech Stack:** .NET 10, ASP.NET Core Controllers, ASP.NET Core Identity, JwtBearer, EF Core 10, Npgsql/PostgreSQL, xUnit v3, WebApplicationFactory, Testcontainers PostgreSQL.

**Spec:** `docs/security/AUTHENTICATION_GUIDE.md`

**Status:** Implemented and verified on 2026-09-07. Release build passed, all 156 tests passed (including PostgreSQL/Testcontainers), setup tests passed, and EF reports no pending model changes. The task checkboxes below are retained as the original TDD execution outline.

## Global Constraints

- API route prefix remains `/api/v1`; requested `/api/auth/*` routes map to `/api/v1/auth/*`.
- Access tokens use RS256, a configured `kid`, 10-minute default lifetime and at most 30-second clock skew.
- Refresh tokens are opaque `{uuid}.{base64url-secret}` values with at least 256 bits of entropy; only HMAC-SHA256 output is persisted.
- Refresh cookie is `HttpOnly`, `Secure`, host-only, `Path=/`, essential, and configurable as `Lax`, `Strict`, or `None`.
- Rotation is strict: no grace window; reuse revokes the entire session.
- PostgreSQL row locking and one-active-token-per-session partial unique index are mandatory.
- Existing user changes and migrations are preserved; only a forward migration is added.
- No real secret, raw token, authorization header or cookie is logged or committed.

---

### Task 1: Freeze authentication contracts and security configuration

**Files:**
- Modify: `docs/api/API_CONTRACT.md`
- Modify: `docs/security/THREAT_MODEL.md`
- Modify: `docs/adr/0001-mvp-contract-decisions.md`
- Create: `src/Portfolio.Infrastructure/Authentication/RefreshTokenOptions.cs`
- Create: `src/Portfolio.Infrastructure/Authentication/RefreshTokenOptionsValidator.cs`
- Modify: `src/Portfolio.Infrastructure/Authentication/JwtOptions.cs`
- Modify: `src/Portfolio.Infrastructure/Authentication/JwtOptionsValidator.cs`
- Test: `tests/Portfolio.IntegrationTests/Authentication/AuthenticationOptionsValidatorTests.cs`

**Interfaces:**
- Produces validated JWT certificate/key-ring settings, refresh lifetimes, HMAC pepper, cookie SameSite mode and rate-limit settings.

- [ ] Write validator tests for missing key ID/certificate, invalid 10-minute lifetime/skew, short pepper, invalid idle/absolute lifetimes and invalid cookie settings.
- [ ] Run the narrow tests and confirm failure because the new options/contracts do not exist.
- [ ] Implement the strongly typed options and validators without adding packages.
- [ ] Run the narrow tests until green.

### Task 2: Add session domain models and PostgreSQL schema

**Files:**
- Create: `src/Portfolio.Application/Common/Models/AuthenticationModels.cs`
- Modify: `src/Portfolio.Infrastructure/Authentication/ApplicationUser.cs`
- Create: `src/Portfolio.Infrastructure/Persistence/Configurations/AuthenticationConfigurations.cs`
- Modify: `src/Portfolio.Infrastructure/Persistence/PortfolioDbContext.cs`
- Create: `src/Portfolio.Infrastructure/Persistence/Migrations/<timestamp>_AddRefreshTokenSessions.cs`
- Test: `tests/Portfolio.IntegrationTests/Persistence/AuthSessionPersistenceTests.cs`
- Modify: `tests/Portfolio.IntegrationTests/Persistence/MigrationTests.cs`
- Modify: `tests/Portfolio.IntegrationTests/Infrastructure/PostgreSqlFixture.cs`

**Interfaces:**
- Produces `AuthSession`, `RefreshToken`, `ApplicationUser.AuthVersion`, `ApplicationUser.IsDisabled`, their mappings, foreign keys, indexes and partial unique active-token index.

- [ ] Write PostgreSQL tests for relationships, cascade behavior, UTC timestamps and rejection of two active refresh tokens in one session.
- [ ] Run them red against the current two-migration schema.
- [ ] Implement models/configurations and generate a forward EF migration.
- [ ] Inspect migration SQL/model snapshot; never edit an old migration.
- [ ] Run the migration/persistence tests green.

### Task 3: Implement opaque token protection and RS256 access tokens

**Files:**
- Modify: `src/Portfolio.Application/Common/Authentication/AccessToken.cs`
- Modify: `src/Portfolio.Application/Common/Authentication/AuthenticatedUser.cs`
- Modify: `src/Portfolio.Application/Common/Authentication/IAccessTokenIssuer.cs`
- Create: `src/Portfolio.Application/Common/Authentication/IRefreshTokenProtector.cs`
- Create: `src/Portfolio.Application/Common/Authentication/ICsrfTokenService.cs`
- Create: `src/Portfolio.Infrastructure/Authentication/RefreshTokenProtector.cs`
- Create: `src/Portfolio.Infrastructure/Authentication/SessionCsrfTokenService.cs`
- Create: `src/Portfolio.Infrastructure/Authentication/JwtKeyRing.cs`
- Modify: `src/Portfolio.Infrastructure/Authentication/JwtAccessTokenIssuer.cs`
- Test: `tests/Portfolio.UnitTests/Authentication/RefreshTokenProtectorTests.cs`
- Modify: `tests/Portfolio.IntegrationTests/Authentication/JwtAccessTokenIssuerTests.cs`

**Interfaces:**
- `IAccessTokenIssuer.Issue(AuthenticatedUser user, Guid sessionId)` emits required JWT claims.
- `IRefreshTokenProtector` generates/parses/verifies selector-secret tokens using fixed-time comparison.
- `ICsrfTokenService` signs/verifies a token bound to the session ID.

- [ ] Write red tests for entropy/format, non-persistence of raw values, wrong-secret rejection, RS256 algorithm, `kid`, `typ`, `iss/aud/sub/sid/jti/iat/nbf/exp/role/auth_version` and 10-minute expiry.
- [ ] Implement the minimal cryptographic services using BCL cryptography.
- [ ] Run the narrow tests green and refactor duplicated Base64Url/HMAC handling.

### Task 4: Implement authentication session use cases and atomic rotation

**Files:**
- Create: `src/Portfolio.Application/Authentication/AuthContracts.cs`
- Modify: `src/Portfolio.Application/Authentication/IAuthService.cs`
- Modify: `src/Portfolio.Application/Authentication/AuthService.cs`
- Create: `src/Portfolio.Application/Authentication/IAuthSessionRepository.cs`
- Create: `src/Portfolio.Application/Common/Authentication/IIdentitySessionAccessor.cs`
- Create: `src/Portfolio.Infrastructure/Persistence/Repositories/AuthSessionRepository.cs`
- Modify: `src/Portfolio.Infrastructure/Authentication/IdentityAuthenticator.cs`
- Create: `src/Portfolio.Infrastructure/Authentication/IdentitySessionAccessor.cs`
- Test: `tests/Portfolio.UnitTests/Authentication/AuthServiceTests.cs`
- Test: `tests/Portfolio.IntegrationTests/Persistence/AuthSessionRotationTests.cs`

**Interfaces:**
- Login returns an access token plus cookie-only refresh material and session-bound CSRF token.
- Refresh performs one `SELECT ... FOR UPDATE` transaction and returns success, invalid, or reuse outcomes.
- Logout/revoke operations are idempotent and ownership-scoped.

- [ ] Write red service tests for login/session creation, generic failure, refresh outcomes, logout, logout-all and IDOR-safe revocation.
- [ ] Write red PostgreSQL tests proving strict rotation, reuse revocation and at most one successful parallel refresh.
- [ ] Implement use cases and repository transaction using the scoped `PortfolioDbContext`.
- [ ] Add structured security events containing safe IDs/outcomes only.
- [ ] Run unit and PostgreSQL tests green.

### Task 5: Add HTTP endpoints, secure cookies, Origin/CSRF, CORS and rate limiting

**Files:**
- Modify: `src/Portfolio.Api/Controllers/AuthController.cs`
- Create: `src/Portfolio.Api/Authentication/AuthCookieWriter.cs`
- Create: `src/Portfolio.Api/Authentication/CurrentUserAccessor.cs`
- Create: `src/Portfolio.Api/Authentication/AuthOriginValidationMiddleware.cs`
- Create: `src/Portfolio.Api/Configuration/AuthRateLimitOptions.cs`
- Modify: `src/Portfolio.Api/Extensions/ServiceCollectionExtensions.cs`
- Modify: `src/Portfolio.Api/Program.cs`
- Modify: `src/Portfolio.Api/appsettings.json`
- Modify: `src/Portfolio.Api/appsettings.Development.json`
- Test: `tests/Portfolio.IntegrationTests/Api/AuthApiTests.cs`

**Interfaces:**
- Adds `POST login`, `POST refresh`, `POST logout`, `POST logout-all`, `GET sessions`, `DELETE sessions/{sessionId}` under `/api/v1/auth`.
- State-changing browser endpoints require exact configured Origin; all except initial login require `X-CSRF-Token` bound to current session.

- [ ] Write red API tests for no-store headers, cookie flags, Origin rejection, CSRF rejection, CORS credentials, 401/403 and endpoint shapes.
- [ ] Implement thin controllers, host-only cookie writer, exact-Origin middleware and partitioned IP rate limits; retain Identity lockout as the account limiter.
- [ ] Configure JwtBearer to accept only RS256/known `kid`/JWT type and validate active session/auth version after cryptographic validation.
- [ ] Run API tests green.

### Task 6: Complete negative JWT and end-to-end security tests

**Files:**
- Modify: `tests/Portfolio.IntegrationTests/Authentication/JwtAccessTokenIssuerTests.cs`
- Modify: `tests/Portfolio.IntegrationTests/Api/AuthApiTests.cs`
- Modify: `tests/Portfolio.IntegrationTests/Api/ProfileAboutApiTests.cs`

**Interfaces:**
- Proves invalid signature, issuer, audience, algorithm, expiry and missing mandatory claims are rejected; revoked sessions/auth-version mismatches cannot call admin endpoints.

- [ ] Add each negative test with independently generated invalid input.
- [ ] Run each test red against the incomplete validator branch.
- [ ] Complete validation/session checks with generic ProblemDetails responses.
- [ ] Run authentication, API and authorization suites green.

### Task 7: Update developer and production documentation

**Files:**
- Modify: `docs/security/AUTHENTICATION_GUIDE.md`
- Modify: `docs/architecture/CURRENT_PROCESS_FLOWS.md`
- Modify: `docs/architecture/NEW_MEMBER_BACKEND_GUIDE.md`
- Modify: `docs/database.md`
- Modify: `docs/LOCAL_SETUP.md`
- Modify: `docs/BACKEND_SPEC.md`
- Modify: `scripts/Setup-Local.ps1`

**Interfaces:**
- Documents key/certificate generation and rotation, refresh/cookie/CSRF contract, Axios single-flight integration, migration/runbook and remaining MFA limitation.

- [ ] Replace access-only statements and the stale About/users statement with implemented behavior.
- [ ] Add sequence diagrams for login, rotation, reuse, logout and session revocation.
- [ ] Add configuration placeholders only; never example secret material.
- [ ] Update setup validation and User Secrets sync for all new non-secret/secret keys.
- [ ] Validate all local Markdown links and script tests.

### Task 8: Quality gate and security review

**Files:**
- Review all changed files; no new feature files.

**Interfaces:**
- Produces executable evidence for build, tests, formatting, migration integrity and secret/token hygiene.

- [ ] Run `dotnet restore`.
- [ ] Run `dotnet build --configuration Release --no-restore`.
- [ ] Run unit tests and all PostgreSQL/API integration tests.
- [ ] Run `dotnet format --verify-no-changes` and fix only formatting from this change.
- [ ] Run `dotnet ef migrations list --project src/Portfolio.Infrastructure --startup-project src/Portfolio.Api`.
- [ ] Inspect `git diff --check`, changed-file scope, cookie/CORS/CSRF configuration, raw-token persistence and log statements.
- [ ] Scan tracked diff for certificate private keys, passwords, token values and realistic secrets.
- [ ] Record any confirmed reusable bug lesson only after its regression test is green.
