# Sprint 9 Security Release Checklist

This is the release evidence index for the CG03 AboutMe MVP. Automated evidence is repository-verifiable. Items marked **Production gate** require the named release owner to record production-equivalent evidence before deployment; they are not represented as passed by local tests.

## Risk closure

| Risk | Status | Evidence | Production owner/action |
| --- | --- | --- | --- |
| SEC-01 public disclosure | Automated | `AdminAuthorizationTests.EveryAdminOperationEnforcesAdministratorPolicy`; `PublicLeakTests.MissingRequestedTranslationsNeverFallBackAcrossPublicFeatures`; `ProjectRepositoryTests.PublicListFiltersDraftsAndOrdersFeaturedThenDisplayOrderThenId`; `ProjectRepositoryTests.LimitedDetailDoesNotProjectSensitiveDataOrGallery`; `ProjectsApiTests.LimitedProjectDetailOmitsSensitiveJsonProperties`; `ProfileAboutApiTests.HiddenProfileContactPropertiesAreAbsentFromPublicJson`; `CertificatesApiTests.HiddenCredentialIdIsAbsentFromPublicJson`; `ResumesApiTests.PublicCurrentReturnsSignedUrlWithoutObjectKey`; feature repository draft/locale tests | None after the automated suite passes. |
| SEC-02 login abuse | Automated | `AuthApiTests.LoginIsRateLimitedByClientIp`; `AuthenticationFlowTests.LoginUsesGenericFailureForMissingWrongOrDisabledAccount`; Identity lockout configuration in `DependencyInjection.cs` | Confirm reverse proxy does not overwrite `RemoteIpAddress` from untrusted forwarding headers. Release operator. |
| SEC-03 alternate database access | **Production gate** | [`database-access.sql`](../supabase/database-access.sql) and its final privilege query | Database owner runs the SQL with the production migration identity, then proves `anon`/`authenticated` cannot select application tables and `portfolio_api` can execute an EF read/write transaction. Release operator. |
| SEC-04 unsafe upload | Automated | `FileValidationTests` zero-byte, size, MIME/signature, sanitized-name and active-SVG cases; feature upload tests verify generated keys | Confirm provider-side bucket MIME/size settings match [`STORAGE_ACCESS.md`](../supabase/STORAGE_ACCESS.md). Release operator. |
| SEC-05 orphaned new object | Automated | `StorageReplacementTests.DeletesNewObjectWhenPersistenceFails`; certificate/project/resume compensation tests | Monitor reconciliation error event IDs 2401, 2604, 2703, and 3406. Operations owner. |
| SEC-06 old object deletion order | Automated | `StorageReplacementTests.KeepsOldObjectUntilNewMetadataCommits`; feature replacement/delete ordering tests | None after the automated suite passes. |
| SEC-07 Contact abuse | Automated | `ContactsApiTests.OversizedContactBodyReturnsPayloadTooLarge`; `SpoofedForwardedHeadersDoNotBypassConnectionRateLimit`; honeypot/validation/notification tests | Review Contact rate values against expected traffic. Release operator. |
| SEC-08 IP spoofing | Automated plus production gate | `ContactsApiTests.SpoofedForwardedHeadersDoNotBypassConnectionRateLimit`; rate partitions use normalized `RemoteIpAddress` | Record trusted proxy/network configuration or record that forwarded headers are disabled. Release operator. |
| SEC-09 secret leakage | Automated plus production gate | `ProductionStartupTests`; source-generated audit templates; safe provider-error contract tests; repository configuration contains no credential values | Run secret scanning and the hostile/error log search described below. Rotate any exposed value before proceeding. Release operator. |
| SEC-10 authorization spoofing/IDOR | Automated | Dynamic full-route admin matrix plus request DTO/OpenAPI review showing no `userId`, `ownerId`, or `isAdmin` permission inputs | None for the single-Portfolio MVP. |
| SEC-11 signed URL lifetime | Automated plus production gate | Certificate and Resume service tests assert five-minute lifetime; Storage adapter contract test verifies the provider request lifetime | Run private-bucket expiry smoke step in [`STORAGE_ACCESS.md`](../supabase/STORAGE_ACCESS.md). Release operator. |
| SEC-12 stored XSS | Contract/review | MVP fields are plain text; JSON serialization tests use hostile/personal strings as data; no raw-HTML response contract exists | Frontend owner confirms output encoding and no unsafe HTML rendering before release. Accepted residual risk owner: frontend owner. |

## Configuration gate

`ProductionStartupTests` proves startup rejects missing/unsafe PostgreSQL, frontend origin, JWT, Storage, upload, auth rate-limit, Contact rate-limit, and bootstrap settings. Production additionally requires:

- exactly one HTTPS origin in `Frontend__Origins__0`;
- `BootstrapAdmin__Enabled=false` after controlled provisioning;
- separate login credentials that are members of `portfolio_api` and `portfolio_migrator` respectively;
- the signing certificate, certificate password, refresh-token pepper, database passwords, and Storage service-role key only in the platform secret store;
- no production values in `appsettings*.json`, container layers, source control, build logs, or release tickets.

## Database deployment and rollback

Before applying the access script, capture the current ACLs as a restricted release artifact:

```sql
SELECT n.nspname, c.relname, c.relkind, c.relacl
FROM pg_class AS c
JOIN pg_namespace AS n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
ORDER BY c.relkind, c.relname;

SELECT defaclrole::regrole, defaclnamespace::regnamespace, defaclobjtype, defaclacl
FROM pg_default_acl
ORDER BY 1, 2, 3;
```

Apply `database-access.sql` before deploying application credentials. Test with the same login role used by production EF Core. If the API role cannot complete its smoke transaction, apply `database-access-rollback.sql`, restore only exact grants from the captured ACL artifact if required, and keep Data API access denied. Never replace the rollback with a broad `GRANT ALL` to `anon`, `authenticated`, or the runtime role.

## Secret rotation

1. Revoke/replace the exposed credential at its authority: database, signing certificate/key, refresh pepper, Storage service-role key, or bootstrap password.
2. Update the platform secret store, deploy one controlled release, and restart every instance so stale process memory is removed.
3. For JWT key rotation, retain the old public validation certificate only for the maximum access-token lifetime plus clock skew, then remove it. Revoke active sessions if private signing material may have leaked.
4. For refresh-pepper exposure, rotate the pepper and revoke all refresh sessions; existing refresh tokens must not remain valid.
5. Search source history, artifacts, logs, and tickets for the exposed value. Restrict/delete contaminated artifacts according to platform policy.
6. Record credential type, rotation time, operator, affected environments, and verification result—never the value itself.

## Incident diagnostics and log redaction

Use request ID, event ID, resource UUID, route, status, and duration for correlation. Do not copy request bodies, Contact email/message/IP, access or refresh tokens, CSRF values, passwords, connection strings, service-role keys, private object contents, or signed URLs into diagnostics.

For hostile/error test runs, search the captured sink for the exact fictional canary values injected by the test. Required zero-match canaries cover password, bearer token, signed query string, service-role key, connection string, Contact email/body/raw IP, and upload content. A match blocks release and requires credential rotation if the canary was not fictional.

## Final execution record

Record these results in the release ticket:

- `dotnet restore`
- `dotnet build --configuration Release --no-restore`
- `dotnet test --configuration Release --no-build`
- `dotnet format --verify-no-changes`
- database/Data API matrix tester and timestamp
- Storage matrix tester and timestamp
- trusted-proxy decision
- bootstrap disabled confirmation
- residual SEC-12 frontend owner sign-off
