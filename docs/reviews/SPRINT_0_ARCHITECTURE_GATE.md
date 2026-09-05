# Sprint 0 Architecture Gate Review

**Date:** 2026-09-05

**Result:** Accepted for Sprint 1

**Scope:** Documentation and decision review only

## Reviewed sources

- `AGENTS.md`
- `docs/BACKEND_SPEC.md`
- `docs/database.md`
- `docs/BUG_LESSONS.md`
- existing API/Application/Infrastructure source and tests
- initial EF migration and model snapshot
- attached planning demand
- `CONTEXT.md`
- `docs/adr/0001-mvp-contract-decisions.md`
- `docs/api/API_CONTRACT.md`
- `docs/security/THREAT_MODEL.md`
- `docs/testing/TEST_STRATEGY.md`
- `docs/testing/BASELINE.md`

## Architecture gate

| Check | Result | Evidence |
| --- | --- | --- |
| Existing three-project dependency direction is preserved | Pass | ADR GATE-01; no production/project changes |
| Layered Architecture remains distinct from full Clean Architecture | Pass | ADR GATE-01 and API boundary rules |
| Controller -> IService -> Service -> IRepository -> Repository -> DbContext flow is preserved | Pass | ADR consequences and test strategy boundaries |
| No Domain project, CQRS, MediatR, generic repository, or custom unit of work is introduced | Pass | ADR and roadmap constraints |
| ASP.NET Core Identity decision is explicit | Pass | ADR GATE-03 |
| Single-Portfolio authorization model is explicit | Pass | ADR GATE-04 |
| PostgreSQL and Storage trust boundaries are explicit | Pass | ADR GATE-13/GATE-14 and threat model |
| Future chat/Redis/AI work is separated from MVP | Pass | ADR and roadmap Future Sprints F0-F6 |

## Specification traceability

| Source requirement | Frozen contract / implementation sprint |
| --- | --- |
| BE-01 Profile/Hero | API Contract section 3; Sprint 2 |
| BE-02 About | API Contract section 3; Sprint 2 |
| BE-03 Skills | API Contract section 4; Sprint 3 |
| BE-04 Work Experience | API Contract section 5; Sprint 4 |
| BE-05 Projects | API Contract section 6; Sprint 5 |
| BE-06 Certificates | API Contract section 7; Sprint 6 |
| BE-07 CV | API Contract section 8; Sprint 7 |
| BE-08 Contact | API Contract section 9; Sprint 8 |
| BE-09 Admin CRUD | Admin route tables across API Contract sections 2-9; Sprints 2-9 |
| ProblemDetails/API envelope | API Contract section 1; existing foundation plus each feature sprint |
| Supabase Storage/file safety | ADR GATE-14, threat model, Sprints 1/2/3/5/6/7 |
| Logging/observability | ADR GATE-11, threat model, Sprints 2-10 |
| CORS/configuration/secrets | Threat model; Sprints 9-10 |
| Docker/CI/deployment | Roadmap Sprint 10 |
| Unit/integration/regression tests | Test strategy; every implementation sprint |

## Resolved discrepancies

1. The attachment's full Clean Architecture language yields to the repository's simple Layered Architecture.
2. The absent `ARCHITECTURE.md`/`DB_SCHEMA.md` names are not introduced as duplicate sources; existing documents remain authoritative.
3. Supabase Auth ambiguity is resolved in favor of the implemented ASP.NET Core Identity administrator system.
4. Owner-scoped prose is resolved to a single-Portfolio model because content tables have no owner FK.
5. Draft-safe publication requires forward default corrections rather than trusting current `true` defaults.
6. Explicit visibility columns are required for Profile email/phone and Certificate credential ID.
7. Certificate issue-date nullability is corrected forward only after a data precheck.
8. Locale selection has exact validation and no silent fallback.
9. Markdown, cache, durable audit tables, public signup, chat, Redis, and AI remain outside MVP.
10. Direct PostgreSQL/local Identity cannot rely on Supabase `auth.uid()`; the API and production role/grant model form the authorization boundary.
11. Hero image mutation uses a dedicated validated upload route; profile JSON writes cannot set arbitrary media URLs.

## Security gate

| Boundary | Required proof before release |
| --- | --- |
| Public content | Draft, locale, visibility, and limited-disclosure JSON tests |
| Administrator | Complete 401/403/200 route matrix plus login lockout/rate limit |
| PostgreSQL | Constraint/transaction/concurrency tests and production-equivalent role smoke test |
| Storage | File-signature/limit tests, server-generated keys, compensation, bucket access matrix |
| Contact | Validation, rate limit, honeypot, privacy, notification-failure tests |
| Errors/logs | Safe ProblemDetails and captured-log secret/PII assertions |
| Deployment | Fail-fast configuration, secret scan, migration rehearsal, non-root container |

## Database impact approved for Sprint 1 planning

The initial migration is immutable. A new forward migration may implement only these approved corrections:

- `profiles.show_email boolean not null default false`
- `profiles.show_phone boolean not null default false`
- `certificates.show_credential_id boolean not null default false`
- approved author-managed `is_published` defaults changed to false without updating existing rows
- `certificates.issued_date` changed to non-null only after an explicit null-data precheck/backfill decision

RLS/grants and Storage policies remain Sprint 9 operational/security artifacts and must be tested with production-equivalent roles.

## Test gate

- Current baseline: Release build passes with zero warnings/errors; 15/15 tests pass.
- Current limitation: persistence tests inspect EF metadata but do not execute PostgreSQL.
- Sprint 1 entry requirement: use the approved test strategy and introduce a PostgreSQL Testcontainer migration/constraint harness before feature persistence work.
- `docs/BUG_LESSONS.md` contains no existing prevention rule for this sprint.

## Conditions carried into Sprint 1

1. Treat API Contract changes as explicit contract decisions, not incidental implementation edits.
2. Preserve the initial migration and create a new migration for approved corrections.
3. Verify production data state before enforcing certificate issue-date non-nullability.
4. Keep public/private Storage bucket behavior and durable metadata semantics aligned with ADR 0001.
5. Add real PostgreSQL evidence before claiming database behavior works.

No unresolved Sprint 0 decision blocks Sprint 1.
