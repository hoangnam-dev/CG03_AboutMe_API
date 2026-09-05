---
name: cg03-backend-feature
description: >
  Implement, modify, review, or test backend application features in the
  CG03 AboutMe ASP.NET Core 10 backend. Use for Controllers, IService/Service,
  feature-specific IRepository/Repository, DTOs, validators, authorization,
  business logic, CRUD operations, publishing, ordering, CV management,
  contact management, or changes spanning API, Application, and persistence.
  Before implementation, consult relevant recurring bug lessons in
  docs/BUG_LESSONS.md when the affected feature, layer, technology, or failure
  pattern has historical entries. Do not use for database-only migration/index
  work or deployment-only work.
---

# CG03 Backend Feature

## Purpose

Use this skill when implementing or modifying backend features in CG03 AboutMe.

The project uses a simple Layered Architecture.

Preferred runtime flow:

Controller
→ IService
→ Service
→ IRepository
→ Repository
→ PortfolioDbContext
→ PostgreSQL

Do not introduce additional architectural layers without a concrete reason.

## Required context

Before implementing a non-trivial feature:

1. Read the applicable `AGENTS.md`.
2. Read the relevant section of `BACKEND_SPEC.md`.
3. Inspect the existing feature implementation.
4. Inspect related tests.
5. Read `database.md` when persistence is involved.
6. Inspect existing EF Core migrations when schema behavior matters.
7. If `docs/BUG_LESSONS.md` exists, search it for lessons relevant to the
   affected feature, layer, technology, or failure pattern.

Treat current source code and migrations as the implementation source of truth.

If documentation and implementation disagree, identify the discrepancy before
making destructive or architectural changes.

Do not load the entire bug history into working context when only a subset is
relevant.

## Known-bug prevention

Before writing code:

1. Identify the affected feature.
2. Identify the affected layer(s).
3. Identify important technologies involved.
4. Search `docs/BUG_LESSONS.md` for matching:
   - feature;
   - area/layer;
   - tags;
   - framework/technology;
   - failure pattern.
5. Extract only the prevention rules that apply.
6. Treat those prevention rules as implementation constraints.
7. Check whether an existing regression test already covers the historical bug.

Examples:

For a Resume upload task, relevant lessons may include:

- active Resume transaction safety;
- file cleanup after database failure;
- version-counter concurrency;
- MIME validation.

For a Project listing task, relevant lessons may include:

- public/draft filtering;
- translation fallback;
- limited disclosure;
- N+1 query prevention.

Ignore unrelated historical lessons.

If no relevant lesson exists, continue normally.

If implementation uncovers a new reusable bug pattern, after the bug is
confirmed and fixed, use the `cg03-bug-lessons` skill to record it.

## Layer responsibilities

### Controller

Controllers handle HTTP concerns.

They may:
- receive request DTOs;
- perform model binding;
- obtain authenticated user context;
- call application services;
- return appropriate HTTP results.

Controllers must not:
- contain business rules;
- access `PortfolioDbContext`;
- execute EF Core queries;
- execute SQL;
- access storage implementations directly.

Prefer:

Controller
→ IService

### Service

Services implement application/business behavior.

Services may:
- enforce business rules;
- coordinate multiple repositories;
- validate ownership;
- implement publish/unpublish behavior;
- implement ordering/reordering;
- coordinate database and storage operations;
- map entities to response models.

Services depend on abstractions rather than concrete repository implementations.

Prefer:

Service
→ feature-specific IRepository

Do not inject concrete repository classes into services.

### Repository

Repositories implement persistence operations.

They may:
- use `PortfolioDbContext`;
- use EF Core;
- perform projections;
- perform feature-specific queries;
- add/update/remove tracked entities.

Repositories must not contain application business rules.

Use feature-specific repositories.

Good:
- `IProjectRepository`
- `IResumeRepository`
- `IContactRepository`

Avoid generic repositories such as `IRepository<T>` unless an actual requirement
demonstrates value.

## Entity Framework Core

Use EF Core directly inside repository implementations.

`PortfolioDbContext` is the EF Core unit of work.

Do not create a custom Unit of Work abstraction solely to wrap
`DbContext.SaveChangesAsync()`.

For operations that modify multiple entities atomically, use the same scoped
DbContext and EF Core transaction APIs.

Examples:
- reorder operations;
- assigning the active Resume;
- allocating a Resume version;
- coordinated multi-table changes.

Keep transaction boundaries at the application use-case level.

## Dependency Injection

Use constructor injection.

Example dependency direction:

ProjectsController
→ IProjectService
→ ProjectService
→ IProjectRepository
→ ProjectRepository
→ PortfolioDbContext

Register abstractions against implementations through the DI container.

Avoid service locator patterns and manual dependency construction.

Do not call `new` for infrastructure dependencies inside controllers or services.

## SOLID

Apply SOLID pragmatically.

### SRP

Keep:
- HTTP handling in Controller;
- business rules in Service;
- persistence logic in Repository.

### OCP

Introduce abstractions where real variation exists.

Do not create hypothetical extension points without a requirement.

### LSP

Implement interfaces according to their contracts.

### ISP

Prefer small feature-specific interfaces over large generic interfaces.

### DIP

High-level business services depend on abstractions such as repositories and
external-service interfaces.

Infrastructure implements those abstractions.

Do not make application services depend directly on concrete repositories.

## Validation

Separate validation into:

1. Request/DTO validation
2. Application/business validation
3. Database constraints

Examples of DTO validation:
- required;
- length;
- range;
- format.

Examples of application rules:
- duplicate slug;
- ownership;
- publish requirements;
- only one active CV;
- valid resource state transitions.

Examples of database protection:
- unique constraints;
- foreign keys;
- check constraints.

Do not rely only on frontend validation.

## CancellationToken

Propagate `CancellationToken` through the whole request path:

Controller
→ Service
→ Repository
→ EF Core / external services.

Do not replace a supplied token with `CancellationToken.None`.

## Public data

Public endpoints must:
- return only published content;
- respect disclosure rules;
- avoid exposing sensitive/internal fields;
- sanitize limited professional project information server-side.

Do not rely on frontend filtering to protect private data.

## File operations

For file-upload features:

1. validate file existence;
2. validate size;
3. validate extension;
4. validate MIME/content where appropriate;
5. generate the storage key server-side;
6. upload the new object;
7. persist metadata;
8. clean up the new object if persistence fails;
9. delete the previous object only after the new state is safely persisted.

Before implementing file operations, also consult relevant bug lessons for:

- orphaned storage objects;
- deletion ordering;
- zero-byte files;
- unsafe file names;
- duplicate uploads;
- partial failure handling.

Do not trust client-provided filesystem or storage paths.

Do not use the original filename as the storage object key.

## Error handling

Use the application's centralized exception handling and ProblemDetails
conventions.

Do not add local try/catch blocks merely to convert every exception.

Catch exceptions locally only when the layer can:
- recover;
- translate a known infrastructure failure;
- perform necessary cleanup;
- add meaningful context.

Never expose:
- stack traces;
- connection strings;
- JWTs;
- service keys;
- SQL implementation details.

## Testing

Use AAA:

Arrange
Act
Assert

Unit test:
- validators;
- service business rules;
- mapping/pure logic;
- authorization behavior where practical.

Mock abstractions such as repositories or external services when isolating
business logic.

Integration test:
- repository behavior;
- EF Core queries;
- PostgreSQL constraints;
- transactions;
- API/database integration.

Do not use mocked repositories as evidence that EF Core queries work.

When changing code in an area with a recorded bug lesson, inspect the lesson's
regression test and preserve or extend it where appropriate.

## Implementation workflow

For each feature:

1. Understand the requested behavior.
2. Inspect documentation and current implementation.
3. Identify affected layers.
4. Identify authorization requirements.
5. Identify persistence impact.
6. Identify transaction requirements.
7. Search relevant entries in `docs/BUG_LESSONS.md`.
8. Extract applicable prevention rules.
9. Inspect existing regression tests for those lessons.
10. Define or update DTOs.
11. Define/update service abstraction.
12. Implement business logic.
13. Define/update feature-specific repository abstraction.
14. Implement repository with EF Core.
15. Wire DI.
16. Add/update tests.
17. Update OpenAPI if the contract changed.
18. Run verification.

Prefer the smallest coherent implementation.

Do not refactor unrelated code.

## Regression prevention before completion

Before declaring the feature complete:

1. Re-check relevant prevention rules from `docs/BUG_LESSONS.md`.
2. Confirm the new implementation does not reproduce known failure patterns.
3. Run the relevant regression tests.
4. If a new bug was discovered during implementation, do not record it as
   resolved until the root cause and fix are verified.
5. Use `cg03-bug-lessons` after verification when the bug contains a reusable
   lesson.

## Verification

Before declaring a backend feature complete, run the relevant commands:

```bash
dotnet build --configuration Release
dotnet test --configuration Release
```

When applicable also verify:

```bash
dotnet format --verify-no-changes
```

If database changes are involved, inspect migrations.

If Docker/deployment is affected, verify the Docker build.

Report explicitly:

- what was verified;
- which relevant bug lessons were checked;
- which regression tests were run;
- what was not verified.
