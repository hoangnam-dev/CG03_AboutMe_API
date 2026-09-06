# CG03 AboutMe MVP REST API Contract

This document freezes the HTTP contract approved by [ADR 0001](../adr/0001-mvp-contract-decisions.md). Feature implementation must update this document before changing a route, field, validation rule, status code, publication rule, or public disclosure rule.

## 1. Conventions

### Base path and media types

- Base path: `/api/v1`
- JSON requests/responses: `application/json`
- Errors: `application/problem+json`
- File uploads: `multipart/form-data`
- JSON property names: camel case
- Enum values: the exact case-sensitive strings shown here
- UUID values: canonical hyphenated strings
- Dates: `YYYY-MM-DD`
- Timestamps: ISO 8601 UTC timestamps

### Operational health endpoints

Health endpoints are anonymous operational routes outside `/api/v1`:

- `GET /health` is dependency-free liveness and returns 200 while the process can serve HTTP.
- `GET /health/ready` checks all required dependencies and returns 200 when healthy or 503 otherwise.
- `GET /health/supabase` checks PostgreSQL and Supabase Storage and returns 200 only when both are healthy, or 503 otherwise.

The Supabase response contains only aggregate/component status and duration:

```json
{
  "status": "healthy",
  "totalDurationMs": 42.5,
  "checks": {
    "postgresql": {
      "status": "healthy",
      "durationMs": 31.2
    },
    "supabase-storage": {
      "status": "healthy",
      "durationMs": 11.3
    }
  }
}
```

Health responses never include provider messages, exception details, URLs, connection strings, credentials, bucket contents, or object keys.

### Success envelope

Single resource:

```json
{
  "data": {},
  "message": "Success",
  "meta": null
}
```

Collection:

```json
{
  "data": [],
  "message": "Success",
  "meta": {
    "page": 1,
    "pageSize": 20,
    "total": 42,
    "totalPages": 3
  }
}
```

`DELETE` success returns 204 with no response body. Creation returns 201 and a `Location` header naming the administrator read route when such a route exists.

### Problem Details

All errors use:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/v1/contact",
  "requestId": "00-...",
  "errors": {
    "senderEmail": ["The senderEmail field is not a valid e-mail address."]
  }
}
```

`errors` is present only for field validation. Responses never contain stack traces, SQL/provider errors, secrets, tokens other than a successful login token, storage keys, or raw file content.

| Status | Contract use |
| --- | --- |
| 400 | Binding/validation failure, unsupported locale, invalid state transition |
| 401 | Missing/invalid/expired bearer token or invalid login credentials |
| 403 | Authenticated caller lacks `AdminPolicy` |
| 404 | Resource or public projection does not exist |
| 409 | Duplicate slug/name, referenced delete, active Resume delete, state/concurrency conflict |
| 413 | Request/file exceeds configured limit |
| 429 | Login or Contact rate limit exceeded |
| 500 | Unexpected server error with safe detail |
| 503 | Database, token issuance, or required external provider unavailable |

### Authentication and authorization

- Public routes are anonymous.
- Every `/api/v1/admin/**` route requires a valid bearer token with the ASP.NET Core Identity `Admin` role.
- The MVP has one Portfolio. Requests never contain `userId`, `ownerId`, or `isAdmin` authorization fields.
- A non-admin authenticated token receives 403; missing or invalid authentication receives 401.

### Locale

- Translated public endpoints accept `locale=en|vi`.
- Missing `locale` means `en`.
- Any other value, including an empty explicit value, returns 400.
- Public responses contain exactly the requested Translation.
- A Published resource missing the requested Translation is treated as a data-integrity failure: a detail endpoint returns 404; collection endpoints omit the invalid resource and emit a server warning. Publishing prevents this state through normal APIs.

### Pagination and filters

- `page`: integer, default 1, minimum 1.
- `pageSize`: integer, default 20, range 1-100.
- `search`: optional trimmed string, maximum 100 characters; an empty trimmed value is treated as absent.
- `isPublished`: optional boolean on administrator content lists.
- `totalPages` is zero when `total` is zero; otherwise `ceil(total / pageSize)`.
- Filtering and ordering execute in PostgreSQL before pagination.
- Ties use `id` ascending for deterministic pagination.

### Shared request types

Translations are explicit objects, not arbitrary locale dictionaries:

```json
{
  "translations": {
    "en": {},
    "vi": {}
  }
}
```

Draft saves may omit a Translation only where the feature section says so. A transition to Published requires both required Translation objects and required localized fields.

Reorder request:

```json
{
  "items": [
    { "id": "4e274d29-a8eb-4b9e-8ad1-5391ab2f0932", "displayOrder": 0 },
    { "id": "4d0e90ac-d424-45a4-84ef-898587398ac7", "displayOrder": 1 }
  ]
}
```

`items` contains every resource in the target scope exactly once. IDs and non-negative `displayOrder` values are unique, and the write is atomic.

## 2. Authentication and Dashboard

### POST `/api/v1/auth/login`

Anonymous. A named rate-limit policy is added in Sprint 9; ASP.NET Core Identity lockout remains authoritative for credential failures.

Request:

```json
{
  "email": "admin@example.com",
  "password": "not-logged-or-echoed"
}
```

Validation: `email` is required, valid, and at most 320 characters; `password` is required and at most 256 characters.

Success: 200.

```json
{
  "data": {
    "accessToken": "<jwt>",
    "tokenType": "Bearer",
    "expiresAt": "2026-09-05T13:00:00Z"
  },
  "message": "Signed in.",
  "meta": null
}
```

Errors: 400, 401 with generic `Invalid email or password.`, 429, 503.

### GET `/api/v1/admin/dashboard`

Admin. Success: 200 with integer `projects`, `certificates`, `resumes`, and `unreadContacts` fields. Counts include all administrator-visible rows, not only Published rows. `unreadContacts` means `status = New`.

## 3. Profile and About

### Public Profile DTO

```json
{
  "slug": "nguyen-hoang-nam",
  "fullName": "Nguyen Hoang Nam",
  "title": "Backend Engineer",
  "shortBio": "Plain text biography.",
  "location": "Ho Chi Minh City",
  "availability": "Available for selected projects",
  "availableForWork": true,
  "email": "public@example.com",
  "phone": "+84...",
  "avatarUrl": "https://...",
  "heroImageUrl": "https://...",
  "socialLinks": [
    {
      "platform": "github",
      "label": "GitHub",
      "url": "https://github.com/example",
      "iconName": "github",
      "displayOrder": 0
    }
  ]
}
```

`email` is omitted when `show_email = false`; `phone` is omitted when `show_phone = false`. Only Published social links are returned, ordered by `displayOrder`, then `id`.

### GET `/api/v1/portfolio/{slug}/profile?locale=en`

Anonymous. Returns 200 with Public Profile DTO or 404. `slug` is normalized lowercase kebab-case, 1-220 characters.

### GET `/api/v1/admin/profile`

Admin. Returns the complete Profile, both Translations, visibility flags, and all social links, including drafts. Returns 404 before the first Profile is created.

### PUT `/api/v1/admin/profile`

Admin. Upserts the single Profile and returns 200.

```json
{
  "slug": "nguyen-hoang-nam",
  "fullName": "Nguyen Hoang Nam",
  "email": "admin@example.com",
  "phone": "+84...",
  "showEmail": false,
  "showPhone": false,
  "availableForWork": true,
  "translations": {
    "en": {
      "title": "Backend Engineer",
      "shortBio": "Plain text.",
      "location": "Ho Chi Minh City",
      "availability": "Available"
    },
    "vi": {
      "title": "Ky su Backend",
      "shortBio": "Noi dung van ban thuan.",
      "location": "Thanh pho Ho Chi Minh",
      "availability": "San sang"
    }
  },
  "socialLinks": [
    {
      "platform": "github",
      "label": "GitHub",
      "url": "https://github.com/example",
      "iconName": "github",
      "displayOrder": 0,
      "isPublished": true
    }
  ]
}
```

Validation: normalized slug 1-220 lowercase kebab-case; full name 1-150; email valid/max 320 when present; phone max 30; title 1-200 for both locales; short bio max 500; location max 200; availability max 250; social platform 1-50 and unique; label max 100; URL absolute HTTPS; icon name max 100; order non-negative and unique. Setting a visibility flag true requires the corresponding value.

`avatarUrl` and `heroImageUrl` are read-only storage-derived fields. Changes use the dedicated upload endpoints below.

Errors: 400, 401, 403, 409 duplicate slug/platform, 503.

### POST `/api/v1/admin/profile/avatar`

Admin multipart request with one `file` part. Allowed: PNG, JPEG, or WebP; non-empty; configured size limit. Success: 200 with `{ "avatarUrl": "https://..." }`. Errors: 400, 401, 403, 413, 503.

### POST `/api/v1/admin/profile/hero-image`

Admin multipart request with one `file` part. Allowed: PNG, JPEG, or WebP; non-empty; configured size and image-dimension limits. Success: 200 with `{ "heroImageUrl": "https://..." }`. Errors: 400, 401, 403, 413, 503.

### Public About DTO

```json
{
  "content": "Plain text About content.",
  "careerGoal": "Plain text career goal.",
  "yearsOfExperience": 5.5,
  "projectCount": 12,
  "technologyCount": 20,
  "showYearsOfExperience": true,
  "showProjectCount": true,
  "showTechnologyCount": true,
  "showContactSection": true
}
```

When a `show*` counter is false, the corresponding numeric property is omitted.

### GET `/api/v1/portfolio/{slug}/about?locale=en`

Anonymous. Returns 200 only when the Profile exists and About is Published; otherwise 404.

### GET `/api/v1/admin/about`

Admin. Returns complete About with both Translations or 404.

### PUT `/api/v1/admin/about`

Admin. Upserts About for the single Profile and returns 200. The request contains non-negative `yearsOfExperience`, `projectCount`, and `technologyCount`; the four `show*` booleans; `isPublished`; and `translations.en`/`translations.vi` with `content` and `careerGoal`. Publishing requires non-empty `content` in both locales.

## 4. Skills

Enums:

- `level`: `Primary`, `Experienced`, `Familiar`, `Learning`
- `icon.type`: `lucide`, `image`, `text`

Skill write DTO:

```json
{
  "categoryId": "d1e6b31e-5907-4db9-874a-c0bdd13e74f4",
  "level": "Primary",
  "yearsOfExperience": 3.5,
  "icon": { "type": "lucide", "value": "database-zap" },
  "displayOrder": 0,
  "isPublished": false,
  "translations": {
    "en": { "name": "PostgreSQL" },
    "vi": { "name": "PostgreSQL" }
  }
}
```

Category write DTO contains `displayOrder`, `isPublished`, and `translations.en.name`/`translations.vi.name`.

Validation: localized names 1-100; years non-negative when present; order non-negative; text icons trim to 1-6 characters; Lucide icon names are 1-100 lowercase kebab-case and on the approved allowlist; image values are managed Storage paths/public URLs or HTTPS URLs on the configured host allowlist. Publishing requires both localized names and a Published parent category.

### GET `/api/v1/portfolio/{slug}/skills?locale=en`

Anonymous. Returns Published categories containing Published skills only. Category and skill order: `displayOrder`, then `id`.

### Administrator Skills routes

| Method | Route | Success | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/admin/skill-categories` | 200 | All categories, both Translations; display order/id; unpaginated |
| POST | `/api/v1/admin/skill-categories` | 201 | Draft unless explicitly Published with complete Translations |
| PUT | `/api/v1/admin/skill-categories/{id}` | 200 | Full mutable replacement |
| DELETE | `/api/v1/admin/skill-categories/{id}` | 204 | 409 when technologies remain |
| GET | `/api/v1/admin/skills?page=1&pageSize=20&search=&categoryId=&isPublished=` | 200 | Paginated, both Translations |
| GET | `/api/v1/admin/skills/{id}` | 200 | Complete admin DTO |
| POST | `/api/v1/admin/skills` | 201 | Uses Skill write DTO |
| PUT | `/api/v1/admin/skills/{id}` | 200 | Full mutable replacement |
| DELETE | `/api/v1/admin/skills/{id}` | 204 | 409 when referenced by Experience/Project/Certificate |
| PATCH | `/api/v1/admin/skills/reorder?categoryId={id}` | 200 | Atomic full-set category reorder |
| POST | `/api/v1/admin/skills/{id}/icon` | 200 | Existing Skill; multipart PNG/JPEG/WebP/SVG after signature/SVG safety validation |

Search matches localized name case-insensitively. Administrator list order is `displayOrder`, then `id`.

## 5. Work Experience

Enums: `highlightType` is `responsibility` or `achievement`.

Write DTO:

```json
{
  "companyName": "Example Co",
  "employmentType": "Full-time",
  "companyUrl": "https://example.com",
  "startDate": "2024-01-01",
  "endDate": null,
  "displayOrder": 0,
  "isPublished": true,
  "translations": {
    "en": { "position": "Backend Engineer", "location": "Remote", "description": "Plain text." },
    "vi": { "position": "Ky su Backend", "location": "Tu xa", "description": "Van ban thuan." }
  },
  "highlights": [
    { "locale": "en", "highlightType": "achievement", "content": "Reduced latency.", "displayOrder": 0 },
    { "locale": "vi", "highlightType": "achievement", "content": "Giam do tre.", "displayOrder": 0 }
  ],
  "technologyIds": ["..."]
}
```

Validation: company 1-200; employment type max 30; company URL absolute HTTPS; start required; end absent means current; present end is on/after start; position 1-150; location max 200; highlight content non-empty; highlight order unique per locale/type; technology IDs unique and existing. Publishing requires both positions.

### Routes

| Method | Route | Success | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/portfolio/{slug}/experiences?locale=en` | 200 | Published; display order, start descending, id |
| GET | `/api/v1/admin/experiences?page=1&pageSize=20&search=&isPublished=` | 200 | Search company/localized position |
| GET | `/api/v1/admin/experiences/{id}` | 200 | Complete aggregate |
| POST | `/api/v1/admin/experiences` | 201 | Atomic aggregate create |
| PUT | `/api/v1/admin/experiences/{id}` | 200 | Atomic aggregate replacement |
| DELETE | `/api/v1/admin/experiences/{id}` | 204 | Cascades its translations/highlights/links |
| PATCH | `/api/v1/admin/experiences/reorder` | 200 | Atomic full-set reorder |

Public DTO derives `isCurrent` from `endDate == null` and includes only requested-locale Translation/highlights plus public technology summaries.

## 6. Projects

Enums: `kind` is `personal|professional`; `disclosureLevel` is `full|limited`.

Write DTO contains: normalized `slug` (1-220), `internalName` (1-200), kind, disclosure level, optional HTTPS repository/demo URLs, optional `thumbnailImageId`, optional start/end dates with end on/after start, `isFeatured`, non-negative `displayOrder`, `isPublished`, both Translations, unique technology IDs, localized ordered highlights, and image metadata. `thumbnailImageId` must identify an image belonging to the same Project; create requests use null until gallery images exist. The service stores the selected managed image URL in `thumbnail_url`.

Project Translation fields:

| Field | Limit/requirement |
| --- | --- |
| `name` | required to publish; 1-200 |
| `shortDescription` | optional; max 500 |
| `clientContext` | optional plain text; full disclosure only |
| `role` | optional; max 200 |
| `problem` | optional plain text; full disclosure only |
| `solution` | optional plain text; full disclosure only |
| `result` | optional plain text; full disclosure only |

### Public disclosure matrix

| Field | `full` | `limited` |
| --- | --- | --- |
| id, slug, name, shortDescription | Return | Return |
| kind, disclosureLevel, thumbnailUrl, dates, featured | Return | Return |
| role, technologies | Return | Return |
| image URLs and localized alt text | Return | Omit |
| clientContext, problem, solution, result | Return when present | Omit |
| repositoryUrl, demoUrl | Return when present | Omit |
| internalName, storage keys, draft state | Omit | Omit |

### Routes

| Method | Route | Success | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/portfolio/{slug}/projects?locale=en` | 200 | Published; featured descending, display order, id |
| GET | `/api/v1/portfolio/{slug}/projects/{projectSlug}?locale=en` | 200 | Published detail through disclosure projection |
| GET | `/api/v1/admin/projects?page=1&pageSize=20&search=&kind=&isPublished=` | 200 | Search internal/localized name |
| GET | `/api/v1/admin/projects/{id}` | 200 | Complete admin aggregate |
| POST | `/api/v1/admin/projects` | 201 | Draft by default; global normalized slug unique |
| PUT | `/api/v1/admin/projects/{id}` | 200 | Full mutable aggregate update |
| DELETE | `/api/v1/admin/projects/{id}` | 204 | Aggregate deletion and post-commit file cleanup |
| PATCH | `/api/v1/admin/projects/{id}/publish` | 200 | Body `{ "isPublished": true }` |
| PATCH | `/api/v1/admin/projects/reorder` | 200 | Atomic full-set reorder |
| POST | `/api/v1/admin/projects/{id}/images` | 200 | Multipart gallery contract below |

Gallery multipart parts:

- `files`: one or more PNG/JPEG/WebP files within configured count and size limits.
- `metadata`: JSON array with unique `fileIndex`, non-negative unique `displayOrder`, and `altText.en`/`altText.vi` each 1-500 characters.
- Each zero-based `fileIndex` occurs exactly once and references an uploaded part.
- Response contains public image DTOs, never object keys.

Publishing requires both Project names and both alt texts for every public gallery image.

## 7. Certificates

Write DTO:

```json
{
  "issuer": "Example Authority",
  "issuedDate": "2026-01-01",
  "expirationDate": null,
  "credentialId": "ABC-123",
  "showCredentialId": false,
  "credentialUrl": "https://example.com/verify/ABC-123",
  "displayOrder": 0,
  "isPublished": true,
  "translations": {
    "en": { "name": "Cloud Certificate" },
    "vi": { "name": "Chung chi Cloud" }
  },
  "technologyIds": ["..."]
}
```

Validation: issuer 1-200; issued date required; expiration on/after issue date; credential ID max 200; credential URL absolute HTTPS; localized name 1-200; technology IDs unique/existing; publishing requires both names. Public `isExpired` is computed from the current date and expiration date. Public `credentialId` is omitted unless `showCredentialId = true`.

`fileUrl` and `imageUrl` are read-only storage-derived fields. The evidence upload endpoint updates the appropriate field from the validated uploaded content type.

### Routes

| Method | Route | Success | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/portfolio/{slug}/certificates?locale=en` | 200 | Published; display order, issued date descending, id |
| GET | `/api/v1/admin/certificates?page=1&pageSize=20&search=&isPublished=` | 200 | Search issuer/localized name |
| GET | `/api/v1/admin/certificates/{id}` | 200 | Complete admin aggregate |
| POST | `/api/v1/admin/certificates` | 201 | Create |
| PUT | `/api/v1/admin/certificates/{id}` | 200 | Full mutable update |
| DELETE | `/api/v1/admin/certificates/{id}` | 204 | Post-commit private-object cleanup |
| POST | `/api/v1/admin/certificates/{id}/file` | 200 | Multipart PDF/PNG/JPEG/WebP |

Private evidence access uses `downloadUrl` and `downloadUrlExpiresAt` generated at response time. Signed URLs and object keys are not durable public DTO fields.

## 8. Resume / CV

`language` is `en|vi`.

### GET `/api/v1/portfolio/{slug}/cv/current?language=en`

Anonymous. `language` is required. Returns 200 only for a Published Active Resume. Response fields: language, computed version such as `v2026_07`, original filename, content type, file size, requested-language description, `downloadUrl`, and `downloadUrlExpiresAt`. Signed URL lifetime is 5 minutes.

Errors: 400 language, 404 no current Published version, 503 provider unavailable.

### GET `/api/v1/admin/cv?language=&page=1&pageSize=20`

Admin. Returns all versions ordered by language, version year descending, version sequence descending. DTO contains ID, language, computed version, original filename, content type, size, description Translations, publication/active state, and created timestamp; it never contains a storage key.

### POST `/api/v1/admin/cv`

Admin multipart:

- `file`: one non-empty PDF matching extension, declared MIME, and PDF signature.
- `language`: required `en|vi`.
- `descriptionEn`, `descriptionVi`: optional, max 500.
- `isPublished`: optional boolean, default false.
- `isActive`: optional boolean, default false; true requires `isPublished = true`.

Client requests containing `version`, `versionYear`, or `versionSequence` are rejected with 400. Success: 201 with complete admin Resume DTO. Version allocation uses the `Asia/Ho_Chi_Minh` year and a transactionally allocated per-language/year sequence.

### PATCH `/api/v1/admin/cv/{id}/current`

Admin. Body `{ "isCurrent": true }`. The target must be Published. Atomically deactivates the prior Active Resume for that language and activates the target. `false` returns 400 because this route cannot leave a language without a replacement. Success: 200.

### PATCH `/api/v1/admin/cv/{id}/publish`

Admin. Body `{ "isPublished": true|false }`. Unpublishing an Active Resume returns 409 until another Published version is current. Metadata changes do not create a new version. Success: 200.

### DELETE `/api/v1/admin/cv/{id}`

Admin. Deletes a non-active version and performs post-commit private-object cleanup. Deleting an Active Resume returns 409. Success: 204. Replacing file bytes always uses `POST /api/v1/admin/cv`; no overwrite endpoint exists.

## 9. Contact

`status` is `New|Read|Archived` in JSON and lowercase in PostgreSQL.

### POST `/api/v1/contact`

Anonymous request:

```json
{
  "senderName": "Visitor",
  "senderEmail": "visitor@example.com",
  "subject": "Project inquiry",
  "message": "Plain text message.",
  "website": ""
}
```

Validation: sender name 1-150; valid sender email max 320; subject 1-200; message 1-5000; honeypot `website` max 200. The named Contact rate limit runs before persistence. A filled honeypot is marked spam and receives the same external response shape/timing class as a normal submission.

Success: 201 with `{ "id": "...", "receivedAt": "2026-09-05T12:00:00Z" }` and message `Message received.` Submitted personal data is not echoed. Notification runs after persistence; notification failure is logged and does not change the 201 result.

Errors: 400, 413, 429, and 500/503 only when persistence itself fails.

### Administrator Contact routes

| Method | Route | Success | Notes |
| --- | --- | --- | --- |
| GET | `/api/v1/admin/contacts?page=1&pageSize=20&status=&search=` | 200 | Created descending/id; search sender name/email/subject |
| GET | `/api/v1/admin/contacts/{id}` | 200 | Complete message; read has no implicit status mutation |
| PATCH | `/api/v1/admin/contacts/{id}/status` | 200 | Body `{ "status": "Read" }` |
| DELETE | `/api/v1/admin/contacts/{id}` | 204 | Hard delete in MVP |

Status rules:

- `New -> Read|Archived`
- `Read -> New|Archived`
- `Archived -> New|Read`
- Entering `Read` sets `readAt` when absent.
- Returning to `New` clears `readAt`.
- Entering `Archived` preserves `readAt`.

No public Contact read route exists.

## 10. OpenAPI and Compatibility

- Every endpoint declares success plus applicable 400/401/403/404/409/413/429/500/503 responses.
- Multipart parts, limits, enum values, locale defaults, pagination defaults, and conditional public fields are described in generated OpenAPI.
- Existing clients may rely only on this versioned `/api/v1` contract.
- A breaking field, route, or status change requires a documented compatibility decision and a new API version or coordinated migration.
