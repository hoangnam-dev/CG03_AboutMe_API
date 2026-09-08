# CG03 AboutMe MVP Threat Model

This threat model covers the approved portfolio MVP. It is a release gate for Profile/About, Skills, Experience, Projects, Certificates, Resume, Contact, administrator authentication, PostgreSQL, and Supabase Storage. Chat, public signup, Redis, WebSockets, and AI require a separate Phase 2 threat model.

## 1. Security objectives

1. Public callers receive only Published content and only the fields approved for disclosure.
2. Only Administrators mutate Portfolio Content or read Contact Messages.
3. Draft, limited-disclosure, contact, credential, storage, and secret data cannot cross the public boundary.
4. File operations cannot execute active content, escape a bucket prefix, exhaust resources, or corrupt database/storage consistency.
5. PostgreSQL remains the durable metadata source; Supabase Storage remains the byte source; partial failures are recoverable.
6. Logs support investigation without becoming a second sensitive-data store.

## 2. Actors and trust assumptions

| Actor | Trust | Allowed capabilities |
| --- | --- | --- |
| Public visitor | Untrusted | Read approved public projections; submit a Contact Message |
| Administrator browser | Partially trusted | Present a bearer token and invoke administrator routes; browser input remains untrusted |
| ASP.NET Core API | Trusted application boundary | Authenticate, authorize, validate, apply business rules, query PostgreSQL, call Storage |
| PostgreSQL application role | Privileged and server-only | Read/write approved application tables and Identity tables |
| Supabase Storage service credential | Highly privileged and server-only | Write/delete configured bucket objects and create signed document URLs |
| CI/deployment platform | Privileged operational boundary | Build artifacts, inject secrets, run one controlled migration job, deploy |
| Frontend origin | Identified, not trusted for authorization | CORS-approved browser caller; cannot establish permissions by itself |

Assumptions requiring production verification:

- TLS terminates at a trusted reverse proxy/platform and HTTPS is enforced externally and by the API.
- Forwarded headers are accepted only from configured proxy networks.
- Supabase Data API cannot expose application tables to anonymous/authenticated roles.
- Bootstrap administrator credentials are disabled after initial controlled provisioning.
- Storage service credentials and JWT signing keys exist only in secret stores.

## 3. Assets

| Asset | Sensitivity | Required protection |
| --- | --- | --- |
| JWT signing key, bootstrap password, DB connection string, Storage service key | Critical secret | Never committed, returned, or logged; rotate through platform secret store |
| Contact Message body/email/IP hash/user agent | Personal/confidential | Admin read only, bounded retention, no request-body logs |
| Draft content | Confidential until Published | Excluded by public repository predicates/projections |
| Limited Project fields | Contractually sensitive | Omitted server-side from public DTOs |
| Hidden Profile email/phone and Certificate credential ID | Personal/private | Omitted unless explicit visibility flag is true |
| Resume and certificate evidence objects | Private documents | Private buckets, short-lived signed URLs, server-only writes |
| Public images | Public after approval | Server-only writes, validation, stable generated keys |
| Identity password hashes/roles/lockout state | Authentication data | Identity APIs only; no API serialization or logs |
| Audit/security logs | Sensitive operational metadata | Structured minimum context, controlled access/retention |

## 4. Trust boundaries and data flows

```text
Public/Admin Browser
        |
        | HTTPS + JSON/multipart + optional bearer token
        v
ASP.NET Core API
  | validation -> authentication -> authorization -> application service
  |                    |                         |
  |                    v                         v
  |             ASP.NET Identity          feature repository
  |                                              |
  +---------------- Npgsql ----------------------+
                         |
                         v
                    PostgreSQL

API -- server-held credential --> Supabase Storage API --> object bytes
API <-- public URL / signed URL -- Supabase Storage API
API -- structured redacted events --> log sink
```

The API is the authorization boundary for portfolio tables. Local Identity JWTs are not treated as Supabase Auth JWTs and are never forwarded as proof for `auth.uid()` policies.

## 5. Threats and required controls

### Authentication and session threats

| Threat | Attack | Controls | Evidence |
| --- | --- | --- | --- |
| Credential stuffing | Repeated login attempts | Identity lockout, named login rate limit, generic 401, HTTPS | Auth service/API/rate-limit tests |
| Account enumeration | Compare login errors/timing | Same response for unknown email, wrong password, and lockout; no account lookup endpoint | API tests and review |
| JWT forgery/replay | Tampered or stolen bearer token | RS256 only, trusted `kid` key ring, issuer/audience/type/signature/lifetime validation, 30-second skew, 10-minute default lifetime, active-session/auth-version check, TLS | JWT/API integration tests |
| Role spoofing | Client sends `isAdmin` or custom role header | Role comes only from validated server-issued token/Identity role | Request DTO scan and 403 tests |
| Bootstrap persistence | Default bootstrap remains enabled | Disabled by default; release checklist disables after provisioning; strong Identity password rules | Configuration validation and runbook |
| Refresh-token database theft | Stored credential is replayed | Opaque 256-bit secret; database stores only HMAC-SHA256 with a server-held pepper | Protector/persistence tests |
| Refresh replay/concurrency | Consumed token is retried or two rotations race | `SELECT ... FOR UPDATE`, strict one-time rotation, partial unique active-token index, whole-session revoke on reuse | PostgreSQL concurrency tests |
| CSRF on cookie endpoints | Attacker causes refresh/logout from another site | Exact Origin check plus session-bound signed CSRF header; CORS credentials restricted to configured frontend | API Origin/CSRF tests |

Accepted limitation: access tokens remain stateless cryptographic artifacts, but authenticated requests perform an active `sid`/`auth_version` database check so logout, session revoke, disable and logout-all take effect immediately for administrator APIs. This adds one indexed database read per authenticated request; public anonymous requests do not pay that cost. MFA/WebAuthn remains recommended future hardening for the administrator account.

### Authorization and disclosure threats

| Threat | Attack | Controls | Evidence |
| --- | --- | --- | --- |
| Admin IDOR | Guess resource UUID on mutation | Every admin route requires `AdminPolicy`; MVP is explicitly single-Portfolio | Route authorization matrix |
| Draft leak | Public query forgets `is_published` | Dedicated public repository methods/projections; no shared admin DTO | Repository/API leak tests |
| Locale leak/mix | Missing Translation triggers fallback | Exact locale validation; no silent fallback; publish completeness | Locale/publish tests |
| Limited Project leak | Reuse full admin entity/DTO | Separate public full/limited projections and disclosure matrix | Field-level serialization tests |
| Hidden contact/credential leak | Return nullable backing fields without policy | Explicit visibility flags; omit field when false | Public DTO tests |
| Mass assignment | Bind persistence entity or privilege fields | Request DTO allowlists; controllers never bind EF entities | Code review/static scan |

### Database threats

| Threat | Attack/failure | Controls | Evidence |
| --- | --- | --- | --- |
| Alternate Data API bypass | Query Supabase tables outside API | Deny anon/authenticated grants or remove application schema from exposed schemas | Supabase access smoke test |
| Overprivileged DB connection | Stolen connection can alter unrelated schemas | Dedicated least-privilege application/migration roles; separate migration credential | Grant review and connection smoke test |
| SQL injection | Search/filter content reaches SQL | EF parameterization; no interpolated raw SQL; manual SQL uses parameters | Review and hostile-input tests |
| Invariant bypass | Invalid locale/date/order/active Resume written directly | PostgreSQL FK, unique, check, and partial indexes | PostgreSQL integration tests |
| Concurrent corruption | Reorder/resume operations race | Explicit transaction; atomic upsert/returning; conflict handling | Concurrency integration tests |

If table RLS is later enabled, its policies and the production EF role must be tested together before deployment. Enabling RLS without a compatible role model is prohibited because it can either block the API or create a false sense of protection.

### File and Storage threats

| Threat | Attack/failure | Controls | Evidence |
| --- | --- | --- | --- |
| Path traversal/object overwrite | Malicious filename/key | UUID/resource-based key generated by server; original name is sanitized metadata only | File validation/storage tests |
| MIME spoof/active content | Extension disagrees with bytes or SVG contains active content | Extension + declared MIME + signature checks; strict SVG parsing/sanitization when enabled | Table-driven validation tests |
| Resource exhaustion | Huge/many files or slow upload | Request/body/count/size limits, bounded stream handling, provider timeout | 413 and timeout tests |
| Unauthorized document read | Guess private object URL | Private buckets and five-minute signed URLs | Storage access matrix |
| Service-key exposure | Key reaches frontend/logs | Server-only options, redaction, secret scan, never serialize client | Configuration/log tests |
| Orphaned new object | Upload succeeds, DB fails | Best-effort compensating delete plus reconciliation event/runbook | Failure-injection test |
| Lost old object | Old delete occurs before DB commit | Commit new metadata first; old delete only afterward | Ordering test |

Supabase Storage objects are changed only through the Storage API. Application code never edits the managed `storage` schema metadata directly.

### Contact and abuse threats

| Threat | Attack | Controls | Evidence |
| --- | --- | --- | --- |
| Spam/flood | High-rate automated POST | Named partitioned limiter, honeypot, body/field limits, optional Turnstile only after evidence | API tests/load check |
| Partition explosion/spoofing | Arbitrary header creates limiter key | Trusted normalized remote IP only; bounded fallback partition | Proxy/rate tests |
| Stored XSS | HTML/script in message/content | Plain-text contract; JSON encoding; frontend output encoding | Serialization/UI contract review |
| PII leakage | Request/body logged | Serilog request metadata only; no bodies/email/message/IP raw value | Captured-log test |
| Notification data loss | Email provider failure rolls back message | Persist first; notification is best-effort side effect | Service failure test |

`POST /api/v1/contact` is capped at 65,536 bytes and each field has its own smaller semantic limit. Its named fixed-window limiter uses only the normalized connection `RemoteIpAddress`; arbitrary forwarding headers do not create partitions. Deployments may expose forwarded client addresses only through an explicitly trusted proxy/network configuration. The API stores lowercase SHA-256 of the normalized address and never stores or logs the raw address. Notification logs contain only the Contact Message ID and do not attach provider exceptions because provider text can contain submitted personal data.

Contact Message retention is manual for the MVP. Administrator deletion is a hard delete; an automated retention job requires a separately approved duration and operational policy.

### Platform, error, and observability threats

| Threat | Controls | Evidence |
| --- | --- | --- |
| CORS trust confusion | One configured HTTPS production origin; CORS is not authorization | Preflight tests and config validation |
| Error information leakage | Central ProblemDetails; generic unexpected 500; provider errors translated | API exception tests |
| Secret in logs | Source-generated structured events with approved scalar IDs only; no tokens/bodies/URLs with signatures | Captured-log assertions |
| Dependency/supply-chain compromise | Central package versions, reviewed lock/update process, CI restore/build/test and secret scan | CI evidence |
| Unsafe migration rollout | One migration runner, backup/precheck, forward-compatible migration, rollback/runbook | Migration rehearsal |
| Free-tier outage/cold start | Liveness/readiness separation, bounded external calls, 503 translation | Health/failure tests |

## 6. Security test matrix

Every implemented feature adds these applicable cases:

- anonymous public happy path;
- anonymous administrator request -> 401;
- authenticated non-admin administrator request -> 403;
- valid Administrator request -> documented success;
- Draft resource absent from public list/detail;
- unsupported locale -> 400;
- hidden/limited fields absent at JSON-property level;
- hostile text serialized as data, never executable markup;
- invalid UUID/resource -> 404 without existence-sensitive detail;
- oversized upload/request -> 413;
- dependency outage -> safe 503 or documented committed-success behavior;
- cancellation reaches repository/provider without corrupting committed state.

## 7. Security release gate

Sprint 9 cannot close until:

1. SEC-01 through SEC-12 in the roadmap have passing evidence or an explicit accepted-risk owner.
2. The full administrator authorization matrix passes.
3. Public field-level leak tests pass for every content type.
4. Production configuration fails fast without DB, JWT, frontend-origin, Storage, upload, and rate-limit settings.
5. Supabase Data API/database-role and Storage bucket access matrices are smoke-tested with production-equivalent roles.
6. Logs from hostile/error test runs contain no secrets, submitted message bodies, passwords, JWTs, signed URLs, or raw connection strings.
7. Bootstrap administration is disabled and the administrator password is held outside repository/deployment manifests.
