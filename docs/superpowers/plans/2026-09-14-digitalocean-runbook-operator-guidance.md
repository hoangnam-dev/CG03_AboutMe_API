# DigitalOcean Runbook Operator Guidance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the production runbook executable by a new operator without relying on undocumented knowledge about Linux users, secrets, symlinks, deploy-script updates, Supabase privileges, or rollback.

**Architecture:** Keep `DIGITALOCEAN_CICD_RUNBOOK.md` as the end-to-end entry point and link to the focused migration and rollback runbooks for deeper recovery decisions. Document only commands and contracts already represented by the repository's workflow, deploy script, environment template, and database-access SQL.

**Tech Stack:** Ubuntu, OpenSSH, Docker, Nginx, GitHub Actions, Supabase PostgreSQL, Bash, PowerShell.

**Spec:** User-requested clarifications for sections 3.3, 5.1, 7.2, 9.1, and 16 of `docs/operations/DIGITALOCEAN_CICD_RUNBOOK.md`.

## Global Constraints

- Never place production credentials in source control, command output, CI logs, chat, or tickets.
- Keep the CD SSH key restricted to the existing forced deployment command.
- Do not grant database DDL to the application runtime role or restore Data API access with broad grants.
- Do not describe application rollback as database rollback.

---

### Task 1: Expand operator and filesystem guidance

**Files:**
- Modify: `docs/operations/DIGITALOCEAN_CICD_RUNBOOK.md`

**Interfaces:**
- Consumes: Existing `deploy` account, SSH hardening, and directory permission contracts.
- Produces: A clear least-privilege explanation, team-member access model, and directory glossary.

- [x] **Step 1:** Explain why routine deployment does not run as `root` and distinguish the service account from human identities.
- [x] **Step 2:** Document the recommended per-person account/key model and the tradeoff of appending several human keys to the shared `deploy` account.
- [x] **Step 3:** Add a purpose/owner/permission glossary for SSH, shared state, certificates, scripts, and Nginx files.

### Task 2: Document production settings and Supabase access

**Files:**
- Modify: `docs/operations/DIGITALOCEAN_CICD_RUNBOOK.md`

**Interfaces:**
- Consumes: `.env.example`, `database-access.sql`, `SPRINT_9_SECURITY_CHECKLIST.md`, Supabase dashboard values, and the generated JWT PFX.
- Produces: A source-or-generation instruction for every abbreviated `app.env` value and a safe database-role setup/verification procedure.

- [x] **Step 1:** Add a settings table showing where each value comes from, which values are generated, and which must remain stable.
- [x] **Step 2:** Add commands that generate the refresh pepper without printing it and create `app.env` with mode `600`.
- [x] **Step 3:** Add the ACL snapshot, SQL Editor execution, runtime login membership, and privilege verification sequence with its security purpose.

### Task 3: Make repeat operations and rollback executable

**Files:**
- Modify: `docs/operations/DIGITALOCEAN_CICD_RUNBOOK.md`

**Interfaces:**
- Consumes: `.github/workflows/backend-ci.yml`, `scripts/deploy-production.sh`, and `docs/operations/ROLLBACK.md`.
- Produces: Exact symlink checks, deploy-script reinstall steps, and separate automatic/manual/GitHub Actions rollback procedures.

- [x] **Step 1:** Add `test -L`, `readlink`, and wrong-target handling before creating the Nginx symlink.
- [x] **Step 2:** Turn the deploy-script update warning into commands with explicit local/server execution locations and verification.
- [x] **Step 3:** Explain the existing cutover-only automatic rollback and give a manual previous-container procedure with database compatibility gate.
- [x] **Step 4:** Explain that the current workflow has no rollback job and document the safe options for a GitHub-dispatched rollback design without claiming it already exists.

### Task 4: Verify documentation integrity

**Files:**
- Verify: `docs/operations/DIGITALOCEAN_CICD_RUNBOOK.md`

**Interfaces:**
- Consumes: Completed documentation edits.
- Produces: Evidence that headings, links, commands, and secret-redaction guidance are internally consistent.

- [x] **Step 1:** Search for every requested subject and confirm it has a concrete answer.
- [x] **Step 2:** Inspect the diff for accidental credentials, unsupported commands, and unrelated edits.
- [x] **Step 3:** Run repository documentation tests if any exist; otherwise report source/diff verification explicitly.
