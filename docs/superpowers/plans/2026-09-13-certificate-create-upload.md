# Certificate Create Upload Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make certificate creation accept the frontend's multipart `payload` plus `file`, persist the certificate evidence object key, and return signed evidence URLs.

**Architecture:** The API controller parses the JSON `payload` form part and converts `IFormFile` into the application upload contract. `CertificateService` validates and uploads the evidence before the first database commit, persists certificate metadata and the object key together, and compensates by deleting the uploaded object if persistence fails. The existing evidence replacement endpoint remains available.

**Tech Stack:** .NET 10, ASP.NET Core controllers, System.Text.Json, xUnit, Supabase Storage abstraction.

**Spec:** `docs/BACKEND_SPEC.md`, `docs/api/API_CONTRACT.md`, `docs/security/THREAT_MODEL.md`

## Global Constraints

- Keep controller concerns limited to multipart binding and JSON parsing.
- Generate storage keys server-side under `certificates/{certificateId}/`.
- Accept only validated PDF, PNG, JPEG, or WebP certificate evidence.
- Persist the object key in PostgreSQL and return only signed URLs, never object keys.
- Delete a newly uploaded object if the database save fails.
- Preserve all unrelated user changes in the dirty worktree.

---

### Task 1: Add a failing service regression test

**Files:**
- Modify: `tests/Portfolio.UnitTests/Certificates/CertificateServiceTests.cs`
- Modify: `src/Portfolio.Application/Certificates/ICertificateService.cs`
- Modify: `src/Portfolio.Application/Certificates/CertificateService.cs`

**Interfaces:**
- Consumes: `CertificateWriteRequest`, `CertificateEvidenceUpload`
- Produces: `CreateCertificateWithEvidenceAsync(CertificateWriteRequest, CertificateEvidenceUpload, CancellationToken)`

- [x] **Step 1:** Add a test proving create uploads the bytes, assigns the PDF object key before save, and returns `downloadUrl`.
- [x] **Step 2:** Run the focused test and confirm it fails because the create-with-evidence service operation does not exist.
- [x] **Step 3:** Implement the minimal create-with-evidence orchestration with validation, upload, one database save, and compensation cleanup.
- [x] **Step 4:** Run the focused service tests and confirm they pass.

### Task 2: Bind the frontend multipart contract

**Files:**
- Modify: `src/Portfolio.Api/Controllers/CertificatesController.cs`
- Modify: `tests/Portfolio.IntegrationTests/Api/CertificatesApiTests.cs`
- Modify: `docs/api/API_CONTRACT.md`

**Interfaces:**
- Consumes: multipart fields `payload` (certificate write JSON) and `file` (`IFormFile`)
- Produces: HTTP 201 `ApiResponse<CertificateAdminResponse>` containing the applicable signed `downloadUrl` or `imageUrl`

- [x] **Step 1:** Add an API test that posts the exact `payload` plus `file` shape and asserts upload, persisted object-key state, and returned signed URL.
- [x] **Step 2:** Run the focused API test and confirm it fails against the current JSON-only action.
- [x] **Step 3:** Change the create action to multipart, deserialize `payload` with web JSON options, and call the create-with-evidence service operation.
- [x] **Step 4:** Document the multipart create contract and run focused API tests.

### Task 3: Verify and record the reusable lesson

**Files:**
- Modify: `docs/BUG_LESSONS.md`

**Interfaces:**
- Consumes: verified regression results
- Produces: a resolved lesson preventing multipart parts from being silently disconnected from application orchestration

- [x] **Step 1:** Run certificate unit and API integration tests.
- [x] **Step 2:** Run the Release build and full solution tests.
- [x] **Step 3:** Record only the verification actually observed.
- [x] **Step 4:** Inspect the final diff to ensure unrelated work was preserved.
