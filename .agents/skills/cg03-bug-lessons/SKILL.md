---
name: cg03-bug-lessons
description: >
  Record, retrieve, and apply lessons from bugs previously encountered in the
  CG03 AboutMe backend. Use after diagnosing or fixing a bug, when a regression
  occurs, when code review finds a repeatable failure pattern, or before
  implementing code in an area with known historical bugs. Maintain concise,
  reusable lessons in docs/BUG_LESSONS.md so future Codex work can avoid
  repeating the same mistakes.
---

# CG03 Bug Lessons

## Purpose

This skill turns resolved bugs into reusable engineering knowledge.

The objective is not to create a chronological incident diary.

The objective is to capture:

- failure pattern;
- root cause;
- reliable diagnosis;
- correct fix;
- prevention rule;
- regression test;
- affected area.

Future implementation and code-review work should consult these lessons when
the same area or pattern is involved.

## Knowledge file

Use:

`docs/BUG_LESSONS.md`

Create it from the project template if it does not exist.

Do not put the complete bug history in `AGENTS.md`.

`AGENTS.md` contains stable project rules.

`docs/BUG_LESSONS.md` contains evolving engineering lessons.

## When to record a bug

Record a lesson when at least one applies:

- the bug required meaningful investigation;
- the root cause was non-obvious;
- Codex could plausibly generate the same mistake again;
- the bug caused a regression;
- the bug involved framework/runtime behavior that is easy to misuse;
- the bug exposed a missing architectural guardrail;
- the bug exposed a missing validation, transaction, auth, or database rule;
- a regression test was added to prevent recurrence.

Do not record trivial typos or one-off mistakes with no reusable lesson.

## Required evidence

Before adding a lesson:

1. Confirm the observed symptom.
2. Confirm the actual root cause.
3. Confirm the fix or mitigation.
4. Record verification evidence.
5. Distinguish root cause from speculation.

If the root cause is not confirmed, mark the entry as `Investigating` instead of
presenting a guess as fact.

## Entry format

Each lesson should use this structure:

```md
## BUG-YYYY-NNN — Short title

- **Status:** Resolved | Investigating | Superseded
- **Area:** API | Application | Repository | EF Core | PostgreSQL | Auth |
  Storage | Testing | Docker | CI/CD | Other
- **Feature:** Projects | Skills | Experiences | Certificates | Resumes |
  Contacts | Shared | Other
- **First observed:** YYYY-MM-DD
- **Last updated:** YYYY-MM-DD
- **Tags:** `tag-1`, `tag-2`

### Symptom

What was observed.

### Root cause

The confirmed technical reason.

### Why it happened

The incorrect assumption, design gap, framework behavior, or implementation
mistake that allowed the bug.

### Correct fix

The minimal reliable fix.

### Prevention rule

A concise rule future code must follow.

### Regression test

Test name/location, or `Not added` with a reason.

### Verification

Commands, tests, logs, or runtime evidence that verified the fix.

### Relevant files

- `path/to/file.cs`

### Related lessons

- BUG-YYYY-NNN
```

## Writing style

Keep each lesson concise and reusable.

Prefer:

> Always recalculate `windowLength` after moving `left`.

over:

> We spent two hours debugging this and eventually discovered...

Prefer technical cause over narrative.

Do not store:
- secrets;
- passwords;
- tokens;
- connection strings;
- personal data;
- production credentials.

## Prevention rule quality

A prevention rule must be actionable.

Bad:

> Be careful with transactions.

Good:

> When replacing the active Resume, update the old and new active flags in the
> same database transaction.

Bad:

> Check nulls.

Good:

> Public project projection must treat optional translation rows as nullable
> and must not dereference them before locale fallback is applied.

## Regression-test requirement

When practical, every resolved bug should produce a regression test.

Choose the appropriate level:

### Unit test

Use when the bug is caused by:
- validation;
- business rules;
- branching logic;
- mapping;
- pure service logic.

### Integration test

Use when the bug depends on:
- EF Core;
- PostgreSQL constraint behavior;
- transaction behavior;
- query translation;
- repository behavior;
- authentication integration;
- storage adapter integration.

Do not use a mocked repository test to claim that a PostgreSQL-specific bug is
prevented.

## Before implementing new code

When this skill is invoked before implementation:

1. Identify the feature/layer being modified.
2. Search `docs/BUG_LESSONS.md` for matching:
   - feature;
   - layer;
   - tags;
   - technology;
   - failure pattern.
3. Extract only relevant prevention rules.
4. Apply them as implementation constraints.
5. Do not load unrelated historical entries into the working context.

Example:

Task:
> Implement Resume upload.

Relevant lessons may include:
- active Resume transaction;
- storage cleanup after DB failure;
- MIME validation;
- version counter concurrency.

Ignore unrelated lessons about project slug queries.

## After fixing a bug

Use this workflow:

1. Run `systematic-debugging` when the root cause is not already established.
2. Fix the root cause.
3. Add/update regression tests where practical.
4. Verify the fix.
5. Search `docs/BUG_LESSONS.md` for an existing matching lesson.
6. Update the existing lesson if the root cause is the same.
7. Create a new lesson only when it represents a distinct reusable failure
   pattern.
8. Add the prevention rule.
9. Link related lessons if useful.

Avoid duplicate lessons describing the same cause.

## Updating an existing lesson

Update instead of creating another entry when:
- the same root cause appears in another file;
- the fix was incomplete;
- a better regression test was added;
- additional affected features are discovered.

Update:
- `Last updated`;
- affected files/features;
- prevention rule if needed;
- regression test;
- verification.

Keep the original bug ID stable.

## Superseding a lesson

If architecture changes make an old prevention rule obsolete:

- set `Status: Superseded`;
- explain the replacement rule;
- link the newer lesson.

Do not silently delete important historical lessons.

## Bug ID allocation

Use:

`BUG-YYYY-NNN`

Example:

`BUG-2026-001`

To allocate a new ID:

1. use the current year;
2. find the highest existing sequence for that year;
3. increment by one;
4. never reuse deleted/superseded IDs.

## Recurring bug escalation

If the same prevention rule is violated repeatedly, consider promoting it from
`docs/BUG_LESSONS.md` into:

- `AGENTS.md` if it is a stable repository-wide invariant; or
- a relevant project `SKILL.md` if it is a reusable workflow rule.

Do not automatically promote every bug.

Promote only repeated or high-impact lessons.

Example:

Repeated lesson:
> Services must not depend on concrete repositories.

This belongs in `AGENTS.md` because it is an architectural invariant.

One-time lesson:
> A specific Resume migration required backfilling version counters.

Keep this in `BUG_LESSONS.md`.

## Relationship with code review

The `cg03-code-review` skill should consult relevant bug lessons.

When a review detects code that reintroduces a known failure:

1. cite the bug ID;
2. explain the matching pattern;
3. classify severity based on actual impact;
4. recommend the existing prevention rule.

## Verification

Do not mark a lesson `Resolved` merely because code was edited.

Use evidence such as:

```bash
dotnet build --configuration Release
dotnet test --configuration Release
```

and, where applicable:
- integration tests;
- migration inspection;
- reproduction steps;
- deployment/health checks;
- relevant logs.

Record only the verification that was actually performed.
