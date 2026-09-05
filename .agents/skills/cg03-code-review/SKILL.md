---
name: cg03-code-review
description: >
  Review backend code changes in the CG03 AboutMe ASP.NET Core 10 project.
  Use for pull-request review, pre-commit review, feature review, bug-fix review,
  refactor review, architecture review, security review, and regression-risk
  analysis. Focus on correctness, Layered Architecture boundaries, SOLID,
  dependency inversion, EF Core/PostgreSQL usage, API behavior, security,
  test coverage, maintainability, and known recurring bugs recorded in
  docs/BUG_LESSONS.md. Do not rewrite code unless explicitly asked.
---

# CG03 Code Review

## Purpose

Use this skill to review code changes before merge, release, or handoff.

The review must prioritize correctness and regression prevention over style.

The backend architecture is:

Controller
→ IService
→ Service
→ IRepository
→ Repository
→ PortfolioDbContext
→ PostgreSQL

Use feature-specific repositories.

Do not recommend a generic repository or a custom Unit of Work wrapper solely
to duplicate EF Core behavior.

## Required context

Before reviewing non-trivial changes:

1. Read the applicable `AGENTS.md`.
2. Read the relevant section of `BACKEND_SPEC.md`.
3. Read `database.md` when persistence is affected.
4. Read `docs/BUG_LESSONS.md` if it exists.
5. Inspect the changed files.
6. Inspect neighboring code that establishes local conventions.
7. Inspect relevant tests.
8. Inspect migrations when database behavior changed.

Do not review code in isolation when surrounding behavior is necessary to
understand correctness.

## Review order

Review in this order:

1. Correctness
2. Security
3. Data integrity
4. Architecture boundaries
5. Error handling
6. Concurrency and transactions
7. API contract
8. Performance
9. Test coverage
10. Maintainability
11. Style

Do not spend most of the review on naming or formatting while correctness risks
remain unresolved.

## Severity

Classify findings using these levels:

### Critical

Likely to cause:
- security vulnerability;
- authorization bypass;
- destructive data loss;
- secret exposure;
- unrecoverable production failure.

### High

Likely to cause:
- incorrect business behavior;
- data corruption;
- duplicate or inconsistent records;
- broken transaction semantics;
- production failure on a normal path;
- public exposure of non-public data.

### Medium

Likely to cause:
- maintainability problems;
- avoidable performance issues;
- incomplete validation;
- weak error handling;
- missing important tests;
- architectural erosion.

### Low

Minor issue that should not block delivery unless repeated:
- naming;
- small duplication;
- readability;
- localized cleanup.

Do not inflate severity.

## Correctness review

Check whether the implementation actually satisfies the requested behavior.

Look for:
- wrong conditions;
- inverted boolean logic;
- null handling errors;
- off-by-one errors;
- missing edge cases;
- wrong status codes;
- wrong ordering;
- incorrect publish filtering;
- incorrect locale handling;
- incorrect Resume versioning;
- stale-state assumptions;
- accidental overwrites.

Trace important behavior end-to-end when needed:

Controller
→ Service
→ Repository
→ EF Core
→ Database constraint

## Architecture review

Verify layer responsibilities.

### Controller

Must not:
- contain business logic;
- query DbContext;
- execute SQL;
- directly use repository implementations;
- directly call Supabase Storage implementations.

Prefer:
Controller
→ IService

### Service

Must:
- contain business/application rules;
- depend on abstractions;
- coordinate repositories and external service abstractions.

Must not:
- depend on concrete repository implementations;
- contain SQL;
- contain EF Core query details unless the project explicitly establishes an
  exception.

### Repository

May:
- use EF Core;
- use PortfolioDbContext;
- implement projections and feature-specific persistence queries.

Must not:
- contain application business rules.

## SOLID and dependency review

Check SOLID pragmatically.

Pay special attention to DIP:

High-level Service
→ abstraction
← low-level implementation

Examples:

ProjectService
→ IProjectRepository
← ProjectRepository

ResumeService
→ IFileStorage
← SupabaseStorageService

Flag direct high-level dependencies on concrete infrastructure when an existing
project abstraction should be used.

Do not recommend interfaces for every class merely for appearance.

## EF Core and database review

When persistence is involved, inspect:

- tracking vs `AsNoTracking()`;
- projection vs loading entire entity graphs;
- N+1 queries;
- unnecessary `Include`;
- multiple `SaveChangesAsync()` calls;
- transaction requirements;
- concurrency behavior;
- uniqueness assumptions;
- FK behavior;
- cascade/delete behavior;
- indexes supporting new access patterns;
- nullable behavior;
- database constraints matching application assumptions.

Do not assume mocked unit tests prove PostgreSQL behavior.

## Transaction review

Identify operations that must succeed or fail atomically.

Examples:
- reorder;
- Resume version allocation;
- switching active Resume;
- multi-row relationship updates;
- metadata replacement.

Verify that all required database changes share the intended transaction.

Avoid network calls inside open database transactions unless their failure
semantics are intentionally designed.

## File-upload review

Check:
- zero-byte validation;
- file-size limit;
- extension validation;
- actual MIME/content validation where appropriate;
- server-generated storage key;
- unsafe original filename usage;
- cleanup when DB persistence fails;
- old object deletion only after successful new state;
- arbitrary client-supplied storage paths;
- authorization before upload/delete.

## Authentication and authorization review

Check:
- authenticated identity comes from trusted server-side context;
- admin/ownership decisions do not trust client-provided flags;
- `/admin` endpoints enforce the expected policy;
- public endpoints do not return drafts;
- limited project disclosure is sanitized server-side;
- sensitive claims or tokens are not logged.

Pay special attention to IDOR risks.

## Error handling review

Check:
- known failures map to appropriate ProblemDetails/status codes;
- unexpected errors are handled globally;
- local try/catch has a real purpose;
- stack traces and secrets are not returned;
- cleanup paths do not hide the original failure;
- logging preserves useful context.

## CancellationToken review

Check propagation:

Controller
→ Service
→ Repository
→ EF Core / external services

Flag unnecessary replacement with `CancellationToken.None`.

## API contract review

Check:
- route consistency;
- status codes;
- response DTO shape;
- pagination metadata;
- ProblemDetails;
- backward compatibility;
- OpenAPI/Swagger changes when the contract changes.

## Test review

Check whether tests cover behavior, not just implementation details.

Prefer AAA:

Arrange
Act
Assert

Unit tests should cover:
- validators;
- business rules;
- service behavior with mocked abstractions;
- pure mapping/logic.

Integration tests should cover:
- repositories;
- PostgreSQL constraints;
- EF Core behavior;
- transactions;
- API/database integration.

For bug fixes, require a regression test when practical.

A bug fix without a regression test should be explicitly justified.

## Known-bug prevention

If `docs/BUG_LESSONS.md` exists:

1. Search it for the affected feature, layer, technology, and failure pattern.
2. Check whether the new code reintroduces a recorded bug.
3. Reference the relevant lesson in the review finding.
4. If the review discovers a new recurring-risk pattern, suggest recording it
   through the `cg03-bug-lessons` skill.

Do not copy the entire bug log into the review.

## Review output

Start with findings, ordered by severity.

For each finding include:

- Severity
- File and location
- Problem
- Why it matters
- Concrete failure scenario
- Recommended fix

Example:

```text
[High] ResumeService.cs:87

Problem:
The previous active Resume is disabled before the new file metadata is saved.

Why it matters:
If the database insert fails, the user can be left without an active Resume.

Scenario:
Storage upload succeeds, old Resume is disabled, new database insert fails.

Recommendation:
Persist the new Resume and active-state transition atomically, and delete the
old storage object only after the new state is committed.
```

After findings, include:

- Assumptions / unanswered questions
- Testing gaps
- Short summary

If no material problems are found, explicitly say so and list remaining
verification risks.

## Review discipline

Do not:
- rewrite unrelated code;
- recommend major architectural changes without evidence;
- propose patterns only because they are fashionable;
- flag formatting that automated tooling already handles;
- claim a defect without explaining a realistic failure path.

Prefer evidence-backed, actionable findings.
