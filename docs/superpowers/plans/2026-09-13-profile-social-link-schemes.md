# Profile Social-Link Schemes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow Profile social links to use absolute HTTP, HTTPS, and valid `mailto:` targets while continuing to reject malformed or unsupported URLs.

**Architecture:** Keep validation in `ProfileService`, where Profile request business validation already lives. Persist the accepted URL unchanged through the existing repository; no schema or migration change is required because `social_links.url` is text.

**Tech Stack:** .NET 10, C#, xUnit, ASP.NET Core

**Spec:** `docs/api/API_CONTRACT.md`

## Global Constraints

- Preserve the existing Controller → IService → Service → IRepository → Repository flow.
- Keep URL validation server-side and return the existing `ValidationException`/ProblemDetails shape for rejected values.
- Do not accept arbitrary URI schemes.

---

### Task 1: Expand Profile social-link URL validation

**Files:**
- Modify: `tests/Portfolio.UnitTests/Profiles/ProfileServiceTests.cs`
- Modify: `src/Portfolio.Application/Profiles/ProfileService.cs`
- Modify: `docs/api/API_CONTRACT.md`
- Modify: `src/Portfolio.Api/OpenApi/RequestExampleOperationFilter.cs`

**Interfaces:**
- Consumes: existing `ProfileService.UpdateAsync(ProfileUpdateRequest, CancellationToken)`.
- Produces: validation accepting absolute `http://`, `https://`, and `mailto:` with a valid email recipient, while rejecting malformed and unsupported schemes.

- [x] **Step 1: Write failing regression tests**

Add Profile service tests that submit valid requests containing `http://www.linkedin.com/in/example` and `mailto:admin@example.com`, and assert the update succeeds. Add theory cases asserting invalid `mailto:` and unsupported schemes still throw `ValidationException` with a `socialLinks.url` error.

- [x] **Step 2: Run tests to verify RED**

Run: `dotnet test tests/Portfolio.UnitTests/Portfolio.UnitTests.csproj --configuration Release --filter FullyQualifiedName~ProfileServiceTests`

Expected: the HTTP and `mailto:` acceptance regression tests fail at the current HTTPS-only validation branch.

- [x] **Step 3: Implement minimal scheme-aware validation**

Parse the value as an absolute `Uri`. Accept `http` and `https` only when the URI has a host. Accept `mailto` only when its recipient component is a single valid `MailAddress`. Throw the existing field-specific validation exception for every other value.

- [x] **Step 4: Update API documentation and Swagger example**

Document the accepted scheme set in `API_CONTRACT.md` and include a `mailto:` social-link entry in the Profile request example so clients can discover the supported format.

- [x] **Step 5: Verify GREEN and regression safety**

Run the focused Profile tests, then `dotnet build --configuration Release` and `dotnet test --configuration Release`. Expected: zero failures.
