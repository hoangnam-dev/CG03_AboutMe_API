---
status: accepted
date: 2026-09-05
---

# Fix the MVP architecture, scope, and public contract boundaries

CG03 AboutMe will complete the current personal-portfolio MVP using its existing three-project Layered Architecture, single-Portfolio database, ASP.NET Core Identity administrator authentication, REST APIs, direct Npgsql persistence, and Supabase Storage. This decision resolves contradictions between the repository sources of truth and an attached future-looking demand so feature implementation can proceed without silently changing architecture, schema ownership, or scope.

## Decisions

| Gate | Accepted decision | Why |
| --- | --- | --- |
| GATE-01 | Preserve `Portfolio.Api`, `Portfolio.Application`, and `Portfolio.Infrastructure`; do not add a Domain project or full Clean Architecture. | The existing dependency direction is coherent and explicitly selected for this small MVP. |
| GATE-02 | `AGENTS.md`, `docs/BACKEND_SPEC.md`, `docs/database.md`, and applied EF migrations are authoritative. Do not create duplicate `ARCHITECTURE.md` or `DB_SCHEMA.md` sources. | One source for each rule prevents drift. |
| GATE-03 | Keep backend-managed ASP.NET Core Identity with an admin-only login endpoint. Public signup is outside MVP. | `database.md`, source, tests, and the initial migration already implement this choice. |
| GATE-04 | Treat the MVP as one Portfolio administered by any authenticated Administrator. Content has no per-owner FK; profile and project slugs are globally unique. | The approved database is single-Portfolio and cannot support meaningful owner checks without a schema redesign. |
| GATE-05 | Add `profiles.show_email` and `profiles.show_phone`, both non-null and default false, in a new forward migration. | Public contact visibility must be explicit and secure by default. |
| GATE-06 | New administrator-managed publishable content starts as Draft. Publishing requires the resource's required `en` and `vi` translations. | Current database defaults of true can expose incomplete content. Existing rows are not silently unpublished by the correction migration. |
| GATE-07 | Make `certificates.issued_date` required after a deployment precheck proves no null production data remains. | BE-06 requires an issue date and expiration logic depends on it. |
| GATE-08 | Public translated endpoints accept `locale=en|vi`; omission means `en`; unsupported values return 400; a missing requested translation is not replaced silently. | Deterministic locale behavior avoids mixed-language responses and hidden data-quality failures. |
| GATE-09 | Treat all text as plain text in MVP. | No Markdown fields or sanitizer contract has been approved. Clients encode text when rendering. |
| GATE-10 | Do not add application caching in MVP. | There is no measured cache need or invalidation design. Database queries remain the source of responses. |
| GATE-11 | Emit structured administrator-operation audit events through Serilog. A durable audit table remains outside MVP. | `database.md` omits `audit_logs` and explicitly defers full audit logging. |
| GATE-12 | Provide explicit administrator read/list endpoints defined in `docs/api/API_CONTRACT.md`. Lists use `page`, `pageSize`, and only documented filters. | Admin UI workflows require reads, while existing feature tables specify mostly mutations. |
| GATE-13 | The API is the only application path to portfolio tables. Production uses a dedicated direct-PostgreSQL role; Supabase Data API access to the application schema is denied. Storage access uses bucket policies and server-held credentials. | Local Identity JWT subjects do not map to Supabase `auth.uid()`, so application authorization is the enforceable content boundary. |
| GATE-14 | `avatars` and `project-images` are public-read/server-write buckets. `certificate-files` and `cv-files` are private/server-write buckets exposed through short-lived signed URLs. | Public media benefits from stable cacheable URLs; documents need revocable access boundaries. |
| GATE-15 | Deleting a skill category or technology that is still referenced returns 409. | Professional history must not be cascade-deleted accidentally. |
| GATE-16 | Define `IContactNotifier`; use a logging/no-op MVP implementation until an email provider is approved. Persistence succeeds independently of notification. | A missing provider cannot block Contact delivery or justify an invented dependency. |
| GATE-17 | Add `certificates.show_credential_id`, non-null and default false, in the Sprint 1 forward migration. | BE-06 requires credential IDs to be public only when explicitly allowed, but the current schema has no visibility state. |
| GATE-18 | Add `POST /api/v1/admin/profile/hero-image` as a dedicated validated upload endpoint; keep `heroImageUrl` read-only in JSON writes. | Hero media is in MVP/schema, but no safe mutation route exists and arbitrary client storage URLs are prohibited. |
| GATE-19 | Extend backend-managed Identity with RS256 access tokens and PostgreSQL-backed opaque rotating refresh-token sessions. Keep the access token in frontend memory and the refresh token in a host-only HttpOnly cookie protected by exact-Origin and session-bound CSRF validation. | This provides bounded browser sessions, rotation/reuse detection and explicit revocation without introducing Supabase Auth or storing raw tokens. |

## Considered Options

- Full Clean Architecture was rejected because it adds projects and indirection without a current domain need.
- Supabase Auth was rejected for this MVP because local Identity is already implemented and migrated.
- Per-owner content authorization was rejected because the database models one Portfolio and has no owner relationships.
- Email-based profile visibility through `site_settings` was rejected because typed columns make privacy defaults and projections explicit.
- Publishing by database default was rejected because bilingual completeness is an application rule evaluated only at publish time.
- Public document buckets were rejected for Resume and certificate evidence because signed private access matches their disclosure requirements.
- Broad table RLS based on `auth.uid()` was rejected because production authentication is not Supabase Auth. Denying alternate Data API access and restricting the backend database role produces a testable trust boundary.
- Durable audit tables, caching, Markdown, public signup, chat, Redis, and AI were rejected from MVP scope because their contracts and operational models are not approved.

## Consequences

- Sprint 1 requires a forward migration for profile visibility, certificate credential visibility, Draft defaults, and certificate issue-date nullability. The existing initial migration remains unchanged.
- All feature controllers use the contracts in `docs/api/API_CONTRACT.md`; changes require updating that contract before code.
- Public repositories use dedicated projections that enforce publication, requested locale, visibility, and limited-disclosure rules in SQL or projection mapping.
- Admin mutation authorization is `AdminPolicy`; there is no fictional content-owner check in the single-Portfolio MVP.
- Public signup and all realtime/chat capabilities require the separately approved Phase 2 specification described in the roadmap.
- Supabase database grants/Data API exposure and Storage bucket policies must be documented and smoke-tested before production release.
