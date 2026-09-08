# CG03 AboutMe MVP Test Strategy

This strategy defines executable evidence for the contracts in `docs/api/API_CONTRACT.md`, the database invariants in `docs/database.md`, and the security controls in `docs/security/THREAT_MODEL.md`.

## 1. Test principles

- Use xUnit v3 and Arrange-Act-Assert.
- Express deterministic business behavior as a failing test before implementation.
- Unit tests isolate Application services at repository, storage, notification, clock, and authentication boundaries.
- PostgreSQL behavior is proven with PostgreSQL, never EF InMemory or mocked repositories.
- API behavior is proven through `WebApplicationFactory<Program>`.
- Ordinary PR tests require no live Supabase, email, or deploy service.
- Provider contract tests use deterministic local HTTP handlers; staging smoke tests run separately with opt-in credentials.
- Tests assert externally meaningful state/status/fields, not private method calls or EF implementation details.
- Every regression fix proves red before the fix and green after it, then records a reusable lesson when applicable.

## 2. Suites and responsibilities

| Suite | Project/location | Proves | Does not prove |
| --- | --- | --- | --- |
| Application unit | `tests/Portfolio.UnitTests/<Feature>` | Validation, business rules, mapping, branching, compensation decisions | EF SQL/constraints/transactions |
| Infrastructure unit/contract | `tests/Portfolio.UnitTests` or focused Integration folder | Options validation, HTTP adapter request/response translation, pure file signatures | Live provider availability |
| PostgreSQL integration | `tests/Portfolio.IntegrationTests/Persistence` | Migrations, SQL translation, indexes/constraints, transactions, concurrency | Browser behavior |
| API integration | `tests/Portfolio.IntegrationTests/Api` | Routing, binding, JSON, auth, ProblemDetails, CORS, rate limits, multipart | Supabase production policies unless opted in |
| Security matrix | `tests/Portfolio.IntegrationTests/Security` | 401/403, draft/field leaks, hostile inputs, limits | External penetration test |
| Supabase staging smoke | opt-in CI/release job | Bucket policies, signed URLs, Data API/DB-role access | Deterministic PR behavior |
| Container smoke | Sprint 10 CI/release job | Image startup, health, configuration, critical API flow | Full business regression suite |

## 3. Test infrastructure design

### PostgreSQL fixture

Create `tests/Portfolio.IntegrationTests/Infrastructure/PostgreSqlFixture.cs` in Sprint 1.

- Start one `PostgreSqlContainer` per non-parallel xUnit collection.
- Use a pinned PostgreSQL major compatible with Supabase production.
- Apply EF migrations with `Database.MigrateAsync` for migration/API suites.
- Reset only application data between tests; preserve schema/migration history.
- Seed Identity roles/users through supported Identity APIs, not direct password-hash inserts.
- Expose a connection string through test configuration only; never print its password.
- Fail CI clearly when its required Docker service is unavailable. Local developers may filter unit tests, but CI cannot silently skip PostgreSQL suites.

### API factory

Extend the existing `PortfolioApiFactory` rather than creating one factory per feature.

- Override configuration with test values.
- Replace external provider registrations through DI with deterministic fakes.
- Generate valid admin/non-admin JWTs through the same issuer or a test authentication handler whose claims match production policy semantics.
- Preserve production middleware order for exception handling, CORS, authentication, authorization, rate limiting, and controllers.
- Disable redirects when asserting HTTP status.

### Storage contract fixture

Use a fake `HttpMessageHandler` or local stub server to record method, path, headers, body metadata, cancellation, and response translation.

- Redact the service key in failure output.
- Cover timeout, 401/403, 404 delete-idempotency, 409 conflict, 413/provider limit, 429, and 5xx.
- Never put live Supabase credentials in test source or ordinary PR variables.

### Time and concurrency

- Inject `TimeProvider` for expiration, certificate status, Resume year allocation, and timestamps.
- Use barriers/tasks for concurrency tests instead of arbitrary sleeps.
- Run Resume allocation and reorder concurrency against real PostgreSQL.

## 4. Required test cases by concern

### Shared contract

```text
ApiEnvelopeTests.SuccessUsesDataMessageAndMeta
ProblemDetailsTests.ValidationIncludesErrorsAndRequestId
ProblemDetailsTests.UnexpectedFailureOmitsImplementationDetail
LocaleTests.OmittedLocaleUsesEnglish
LocaleTests.UnsupportedLocaleReturnsBadRequest
PaginationTests.RejectsPageBelowOneAndPageSizeAboveOneHundred
```

### Authentication and authorization

```text
AuthServiceTests.LoginCreatesSessionAndReturnsAccessRefreshAndCsrfMaterial
AuthServiceTests.LoginUsesGenericFailureWhenCredentialsAreInvalid
AuthServiceTests.RefreshRotatesTokenAndReturnsNewAccessToken
JwtAccessTokenIssuerTests.IssuesRs256TokenWithRequiredClaimsAndKeyId
JwtValidationApiTests.* (expiry, issuer, audience, signature, algorithm, required claims)
AuthenticationFlowTests.LoginRotationAndReplayDetectionWorkEndToEnd
AuthenticationFlowTests.LoginUsesGenericFailureForMissingWrongOrDisabledAccount
AuthenticationFlowTests.LogoutRevokesCurrentSessionAndInvalidatesAccessToken
AuthSessionPersistenceTests.ParallelRefreshAllowsAtMostOneRotationAndRevokesSessionOnReuse
AuthSessionPersistenceTests.RefreshRejectsExpiredRevokedOrDisabledState
AuthSessionPersistenceTests.UserCannotRevokeAnotherUsersSession
AuthSessionPersistenceTests.RevokeAllRevokesEverySessionAndIncrementsAuthVersion
AdminAuthorizationTests.AnonymousCallerReceivesUnauthorizedForEveryAdminRoute
AdminAuthorizationTests.NonAdminCallerReceivesForbiddenForEveryAdminRoute
AdminAuthorizationTests.AdminCallerReachesEveryAdminRoute
AuthApiTests.LoginIsRateLimitedByClientIp
AuthApiTests.OriginProtectionCannotBeBypassedWithRouteCasing
```

### Public disclosure

```text
PublicContentTests.DraftsAreAbsentForEveryContentType
PublicContentTests.ResponseContainsRequestedLocaleOnly
ProfileDisclosureTests.HiddenEmailAndPhonePropertiesAreAbsent
ProjectDisclosureTests.LimitedProjectPropertiesAreAbsent
CertificateDisclosureTests.HiddenCredentialIdPropertyIsAbsent
StorageDisclosureTests.ObjectKeysAndServiceCredentialsAreAbsent
```

### Persistence and transactions

```text
MigrationTests.AllMigrationsApplyToEmptyDatabase
MigrationTests.CorrectionMigrationRejectsUnsafeCertificateNullState
ConstraintTests.UnsupportedLocalesAreRejected
ConstraintTests.InvalidDatesAndNegativeOrdersAreRejected
ConstraintTests.OneActiveResumePerLanguageIsEnforced
ReorderTests.InvalidSetRollsBackEveryOrderChange
ResumeConcurrencyTests.ConcurrentUploadsAllocateUniqueSequences
ResumeTransactionTests.FailureRollsBackCounterMetadataAndActiveSwitch
```

### Files and partial failure

```text
FileValidationTests.ZeroByteOversizeAndSignatureMismatchAreRejected
FileValidationTests.ClientFilenameNeverBecomesObjectKey
UploadCompensationTests.DatabaseFailureDeletesNewObject
UploadCompensationTests.NewCommitPrecedesOldObjectDeletion
UploadCompensationTests.CompensationFailureEmitsReconciliationEvent
SignedUrlTests.PrivateDocumentUrlExpiresAfterConfiguredLifetime
```

### Contact abuse and privacy

```text
ContactValidationTests.InvalidEmailAndFieldLimitsReturnBadRequest
ContactHoneypotTests.FilledHoneypotUsesIndistinguishableResponse
ContactRateLimitTests.ExcessTrustedIpRequestsReturnTooManyRequests
ContactNotificationTests.ProviderFailureDoesNotLoseCommittedMessage
ContactPrivacyTests.PublicCallerCannotReadInbox
ContactLogTests.BodyEmailAndRawIpAreAbsentFromLogs
```

Sprint 8 evidence also covers a 65,536-byte body limit, untrusted `X-Forwarded-For` spoofing against one connection partition, required status input, JSON status casing, PostgreSQL status constraints, stable newest-first pagination, and dashboard unread-count consistency.

## 5. Per-change TDD loop

1. Add one test that states one contract/business rule.
2. Run the narrow test and confirm it fails for the expected missing/incorrect behavior.
3. Implement the smallest coherent change through the correct layer boundaries.
4. Run the narrow test and related feature suite until green.
5. Refactor without changing the contract.
6. Run the feature unit, PostgreSQL integration, and API/security suites affected by the change.
7. Update API/OpenAPI/docs in the same change.
8. Run the full Release verification before review.

Completion criterion: the PR description names the red test, final green commands, affected threat controls, migration/storage impact, and documentation updated.

## 6. Test data rules

- Use clearly fictional names/emails and fixed UUIDs.
- Use fixed `TimeProvider` values except tests explicitly exercising real expiration.
- Builders/factories produce valid defaults; each test overrides only the rule under test.
- Never copy production records, credentials, URLs containing signatures, or personal Contact data.
- Keep `en` and `vi` values visibly different so locale-mixing failures are detectable.
- Use unique slugs/names per test unless testing a conflict.

## 7. Verification commands

Fast unit loop:

```powershell
dotnet test tests\Portfolio.UnitTests\Portfolio.UnitTests.csproj --configuration Release
```

PostgreSQL/API integration loop:

```powershell
dotnet test tests\Portfolio.IntegrationTests\Portfolio.IntegrationTests.csproj --configuration Release
```

Full PR gate:

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet format --verify-no-changes
```

Migration changes additionally require:

```powershell
dotnet tool restore
dotnet ef migrations list --project src\Portfolio.Infrastructure --startup-project src\Portfolio.Api
```

Deployment changes additionally require `docker build .` and a container smoke test.

## 8. Coverage and quality gates

- No repository-wide percentage target is imposed before representative feature code exists.
- Every explicit business rule and security disclosure rule has at least one named test.
- Branch coverage is reviewed for publish, partial-failure, authorization, and state-transition logic.
- Mutation or property-based testing may be added only where it reveals gaps in high-risk deterministic rules.
- Flaky tests are defects: fix synchronization/state isolation; do not add retries that hide nondeterminism.
- Live-provider smoke failures block release but do not make ordinary unit tests non-deterministic.

## 9. Regression lessons

Before feature work, search `docs/BUG_LESSONS.md` by feature, layer, technology, and failure pattern. Preserve its named regression test. After verifying a reusable bug fix, add or update one concise lesson with root cause, prevention rule, test path, and executed verification.
