# CG03 AboutMe — Luồng xử lý hiện tại

Để học cách tự triển khai một feature Controller–Service–Repository, xem [Backend Onboarding Guide](NEW_MEMBER_BACKEND_GUIDE.md). Chi tiết Identity tables, Admin bootstrap, login và JWT nằm tại [Authentication Guide](../security/AUTHENTICATION_GUIDE.md).

## 1. Phạm vi

Tài liệu này mô tả hành vi đang được triển khai trong backend đến hết Sprint 2:

- thiết lập local, migration và bootstrap tài khoản Administrator;
- đăng nhập Administrator và phát hành JWT;
- đọc/cập nhật Profile và Social Links;
- đọc/cập nhật About;
- thay thế Avatar và Hero image;
- xử lý lỗi tập trung và ranh giới dữ liệu nhạy cảm.

Nguồn đối chiếu:

- `scripts/Setup-Local.ps1`;
- `src/Portfolio.Api/Program.cs` và các Controller;
- `src/Portfolio.Application/Authentication`, `Profiles`, `About`;
- `src/Portfolio.Infrastructure/Authentication`, `Persistence`, `Storage`;
- `docs/api/API_CONTRACT.md` và `docs/STORAGE.md`.

Các phần Skills, Experiences, Projects, Certificates, Resumes và Contacts có hợp đồng trong `API_CONTRACT.md` nhưng chưa thuộc luồng runtime Sprint 2 nên không được mô tả như chức năng đã hoàn thành ở đây.

## 2. Data Flow Diagram — mức hệ thống

```mermaid
flowchart LR
    Public[Public visitor]
    Admin[Administrator]
    Dev[Developer]

    Setup[Setup-Local.ps1]
    API[ASP.NET Core API]
    Auth[Authentication and AdminPolicy]
    App[Application services]
    Repo[Feature repositories]
    StorageAdapter[Supabase Storage adapter]
    ErrorHandler[Global exception handler]

    Config[(.env or User Secrets)]
    Identity[(ASP.NET Identity tables)]
    Portfolio[(Portfolio PostgreSQL tables)]
    Objects[(Supabase Storage buckets)]
    Logs[(Structured logs)]

    Dev -->|database, JWT and Storage configuration| Setup
    Setup -->|validated process settings| Config
    Setup -->|optional EF migrations| Portfolio

    Public -->|anonymous GET with slug and locale| API
    Admin -->|login credentials or Bearer JWT| API
    Config -->|startup configuration| API

    API --> Auth
    Auth -->|credential verification| Identity
    Auth -->|authorized request| App
    App --> Repo
    Repo -->|EF Core queries and writes| Portfolio
    App --> StorageAdapter
    StorageAdapter -->|server-authorized upload and delete| Objects

    App -->|resource IDs and operation events| Logs
    API --> ErrorHandler
    ErrorHandler -->|safe Problem Details| Public
    ErrorHandler -->|safe Problem Details| Admin

    API -->|localized public DTO| Public
    API -->|admin DTO or JWT| Admin
```

### Quy tắc dữ liệu tại các ranh giới

| Ranh giới | Dữ liệu được phép đi qua | Dữ liệu không được trả/log |
| --- | --- | --- |
| Public API | Published content, đúng locale, contact fields được cho phép | Draft, locale khác, hidden email/phone, object key, secret |
| Admin API | DTO quản trị sau khi JWT có role `Admin` được xác thực | Password, signing key, Storage service-role key |
| PostgreSQL | Entity và object key bền vững | Signed URL tạm thời |
| Supabase Storage | Bucket, server-generated object key, file bytes | Client-supplied storage path |
| Structured logs | Request/trace ID, resource ID, loại thao tác | Nội dung Profile/About, password, JWT, file bytes |

## 3. Luồng kiến trúc request

```mermaid
flowchart TD
    Request[HTTP request]
    Middleware[Exception, CORS, authentication and authorization middleware]
    Controller[Controller]
    Service[IService implementation]
    Repository[IRepository implementation]
    DbContext[PortfolioDbContext]
    PostgreSQL[(PostgreSQL)]
    Response[ApiResponse or Problem Details]

    Request --> Middleware
    Middleware --> Controller
    Controller --> Service
    Service --> Repository
    Repository --> DbContext
    DbContext --> PostgreSQL
    PostgreSQL --> DbContext
    DbContext --> Repository
    Repository --> Service
    Service --> Controller
    Controller --> Response

    Middleware -. exception .-> Response
    Service -. application exception .-> Middleware
    Repository -. infrastructure exception .-> Middleware
```

Controller chỉ xử lý HTTP/model binding. Business rules nằm trong Service. EF Core và SQL nằm trong Repository. `CancellationToken` được truyền xuyên suốt chuỗi gọi.

## 4. Setup, migration và bootstrap Administrator

```mermaid
sequenceDiagram
    autonumber
    actor Dev as Developer
    participant Setup as Setup-Local.ps1
    participant Env as .env / User Secrets
    participant EF as dotnet-ef
    participant API as Portfolio.Api startup
    participant Boot as AdminBootstrapper
    participant Identity as ASP.NET Identity
    participant DB as PostgreSQL

    Dev->>Setup: Chạy setup
    alt .env chưa tồn tại
        Setup->>Dev: Hỏi DB, frontend origin và Storage settings
        Setup->>Setup: Sinh RSA certificate, PFX password và refresh pepper
        Setup->>Env: Tạo .env với bootstrap disabled
    else .env đã tồn tại
        Setup->>Env: Đọc cấu hình hiện có
    end

    Setup->>Setup: Validate cấu hình
    opt -SyncUserSecrets
        Setup->>Env: Đồng bộ sang .NET User Secrets
    end
    Setup->>Setup: Restore tools/packages và Release build
    Setup->>EF: Liệt kê migrations
    opt -ApplyMigrations
        EF->>DB: Áp dụng migrations
    end
    Setup-->>Dev: Setup hoàn tất, không tự khởi động API

    Dev->>API: Khởi động API
    API->>Boot: SeedAsync()
    alt BootstrapAdmin.Enabled = false
        Boot-->>API: Không thay đổi Identity
    else BootstrapAdmin.Enabled = true
        Boot->>Identity: Tạo role Admin nếu thiếu
        Identity->>DB: Ghi role
        Boot->>Identity: Tìm user theo email
        alt User chưa tồn tại
            Boot->>Identity: Tạo user, hash password
            Identity->>DB: Ghi user
        end
        Boot->>Identity: Gán role Admin nếu thiếu
        Identity->>DB: Ghi user-role
        Boot-->>API: Log UserId, không log credential
    end
```

### Điều kiện vận hành

1. Bootstrap mặc định tắt và setup script không trực tiếp tạo tài khoản.
2. Identity tables phải tồn tại trước khi API bootstrap user; database mới cần chạy migrations.
3. Sau lần bootstrap thành công phải đặt `BootstrapAdmin__Enabled=false`, đồng bộ lại cấu hình và restart API.
4. Password thực tế phải thỏa Identity policy: tối thiểu 12 ký tự, có chữ hoa, chữ thường, chữ số và ký tự đặc biệt.

## 5. Đăng nhập Administrator

Endpoint: `POST /api/v1/auth/login`

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Administrator
    participant Controller as AuthController
    participant Service as AuthService
    participant Authenticator as IIdentityAuthenticator
    participant Identity as ASP.NET Identity
    participant SessionRepo as IAuthSessionRepository
    participant Issuer as RS256 IAccessTokenIssuer
    participant DB as PostgreSQL

    Admin->>Controller: email + password
    Controller->>Service: LoginAsync(request)
    Service->>Service: Validate email/password structure
    Service->>Authenticator: AuthenticateAsync(email, password)
    Authenticator->>Identity: Find user and verify password/lockout

    alt Credential không hợp lệ hoặc bị lockout
        Identity-->>Authenticator: Authentication failed
        Authenticator-->>Service: Failure
        Service-->>Admin: 401 generic Problem Details
    else Identity user hợp lệ
        Identity-->>Authenticator: AuthenticatedUser
        Authenticator-->>Service: User ID, email, roles, auth version
        Service->>SessionRepo: Create session + hashed opaque refresh token
        SessionRepo->>DB: Commit auth_sessions + refresh_tokens
        Service->>Issuer: Issue RS256 JWT with sid/auth_version
        Issuer-->>Service: access token and expiry
        Service-->>Controller: access + raw refresh + session-bound CSRF
        Controller-->>Admin: 200 JSON + HttpOnly Secure refresh cookie
    end
```

Access JWT tiếp theo được gửi bằng header `Authorization: Bearer <token>`. Login xác thực Identity account; `AdminPolicy` ở các `/admin` endpoint mới quyết định role `Admin` và trả 403 nếu account hợp lệ không có role đó. Request body hoặc custom header không thể tự khai báo quyền Admin. MVP chỉ bootstrap Admin và không có public signup. Access token chỉ giữ trong memory; refresh token chỉ nằm trong cookie `__Host-refresh`. Luồng rotation/reuse chi tiết nằm trong [Authentication Guide](../security/AUTHENTICATION_GUIDE.md).

## 6. Đọc Public Profile

Endpoint: `GET /api/v1/portfolio/{slug}/profile?locale=en`

```mermaid
sequenceDiagram
    autonumber
    actor Client as Public client
    participant Controller as ProfileController
    participant Service as ProfileService
    participant Repo as ProfileRepository
    participant DB as PostgreSQL
    participant Storage as IFileStorage

    Client->>Controller: slug + optional locale
    Controller->>Service: GetPublicAsync(slug, locale)
    Service->>Service: Normalize slug and locale
    alt Slug hoặc locale không hợp lệ
        Service-->>Client: 400 Problem Details
    else Input hợp lệ
        Service->>Repo: GetPublicAsync(normalizedSlug, locale)
        Repo->>DB: AsNoTracking projection
        Note over Repo,DB: Exact locale; only published social links; order by displayOrder then id
        DB-->>Repo: Profile projection or null
        alt Không có profile hoặc requested translation
            Repo-->>Service: null
            Service-->>Client: 404 Problem Details
        else Tìm thấy
            Repo-->>Service: Projection with visibility flags and media keys
            Service->>Service: Omit email/phone when visibility is false
            Service->>Storage: Resolve public URLs from object keys
            Storage-->>Service: Avatar/Hero public URLs
            Service-->>Controller: PublicProfileResponse
            Controller-->>Client: 200 ApiResponse
        end
    end
```

Public response không chứa Translation collection, locale khác, social link draft hoặc storage object key.

## 7. Đọc Public About

Endpoint: `GET /api/v1/portfolio/{slug}/about?locale=en`

```mermaid
sequenceDiagram
    autonumber
    actor Client as Public client
    participant Controller as AboutController
    participant Service as AboutService
    participant Repo as AboutRepository
    participant DB as PostgreSQL

    Client->>Controller: slug + optional locale
    Controller->>Service: GetPublicAsync(slug, locale)
    Service->>Service: Normalize slug and locale
    Service->>Repo: GetPublicAsync(normalizedSlug, locale)
    Repo->>DB: Query Profile slug + About isPublished + exact locale
    alt Profile/About/translation không tồn tại hoặc About là draft
        DB-->>Repo: null
        Service-->>Client: 404 Problem Details
    else Published About tồn tại
        DB-->>Repo: Localized projection
        Repo-->>Service: AboutPublicProjection
        Service->>Service: Omit numeric counters whose show flag is false
        Service-->>Controller: PublicAboutResponse
        Controller-->>Client: 200 ApiResponse
    end
```

## 8. Đọc dữ liệu quản trị Profile/About

Endpoints:

- `GET /api/v1/admin/profile`;
- `GET /api/v1/admin/about`.

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Administrator
    participant Auth as JWT middleware and AdminPolicy
    participant Controller as Admin Controller
    participant Service as ProfileService or AboutService
    participant Repo as Feature Repository
    participant DB as PostgreSQL

    Admin->>Auth: GET with Bearer JWT
    alt Thiếu hoặc JWT không hợp lệ
        Auth-->>Admin: 401 Problem Details
    else JWT hợp lệ nhưng không có role Admin
        Auth-->>Admin: 403 Problem Details
    else Admin hợp lệ
        Auth->>Controller: Authorized request
        Controller->>Service: GetAdminAsync()
        Service->>Repo: GetAdminAsync()
        Repo->>DB: AsNoTracking aggregate query
        alt Resource chưa tồn tại
            Service-->>Admin: 404 Problem Details
        else Resource tồn tại
            Service-->>Controller: Full admin DTO with both locales
            Controller-->>Admin: 200 ApiResponse
        end
    end
```

## 9. Upsert Profile và Social Links

Endpoint: `PUT /api/v1/admin/profile`

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Administrator
    participant Policy as AdminPolicy
    participant Controller as AdminProfileController
    participant Service as ProfileService
    participant Repo as ProfileRepository
    participant DB as PostgreSQL
    participant Log as Structured logger

    Admin->>Policy: PUT with Bearer JWT and ProfileUpdateRequest
    Policy->>Controller: Authorized request
    Controller->>Service: UpdateAsync(request)
    Service->>Service: Validate name, contact visibility, translations, links and HTTPS URLs
    alt Validation không hợp lệ
        Service-->>Admin: 400 Problem Details with errors
    else Platform hoặc display order bị trùng
        Service-->>Admin: 409 Problem Details
    else Request hợp lệ
        Service->>Service: Normalize slug
        Service->>Repo: GetForUpdateAsync()
        Repo->>DB: Load tracked Profile + translations + social links
        Service->>Repo: SlugExistsAsync(slug, excludedProfileId)
        Repo->>DB: Check duplicate slug
        alt Slug bị trùng
            Service-->>Admin: 409 Problem Details
        else Slug khả dụng
            opt Profile chưa tồn tại
                Service->>Repo: AddAsync(new Profile)
            end
            Service->>Service: Update base fields and merge en/vi translations/social links
            Service->>Repo: SaveChangesAsync()
            Repo->>DB: Commit one EF Core unit of work
            Repo-->>Service: Saved
            Service->>Log: ProfileUpdated(ProfileId)
            Service-->>Controller: ProfileAdminResponse
            Controller-->>Admin: 200 ApiResponse
        end
    end
```

`avatarUrl` và `heroImageUrl` không nằm trong JSON write contract; media chỉ được thay đổi qua endpoint upload chuyên biệt.

## 10. Upsert About

Endpoint: `PUT /api/v1/admin/about`

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Administrator
    participant Policy as AdminPolicy
    participant Controller as AdminAboutController
    participant Service as AboutService
    participant Repo as AboutRepository
    participant DB as PostgreSQL
    participant Log as Structured logger

    Admin->>Policy: PUT with Bearer JWT and AboutUpdateRequest
    Policy->>Controller: Authorized request
    Controller->>Service: UpdateAsync(request)
    Service->>Service: Validate non-negative counters and en/vi translations
    opt Request publishes About
        Service->>Service: Require non-empty content in both locales
    end
    Service->>Repo: GetForUpdateAsync()
    Repo->>DB: Load tracked About + translations

    alt About chưa tồn tại
        Service->>Repo: GetProfileIdAsync()
        Repo->>DB: Read the single Profile ID
        alt Profile chưa tồn tại
            Service-->>Admin: 404 Problem Details
        else Profile tồn tại
            Service->>Repo: AddAsync(new About linked to Profile)
        end
    end

    Service->>Service: Update counters, flags, publish state and translations
    Service->>Repo: SaveChangesAsync()
    Repo->>DB: Commit one EF Core unit of work
    Service->>Log: AboutUpdated(AboutId)
    Service-->>Controller: AboutAdminResponse
    Controller-->>Admin: 200 ApiResponse
```

## 11. Thay thế Avatar hoặc Hero image

Endpoints:

- `POST /api/v1/admin/profile/avatar`;
- `POST /api/v1/admin/profile/hero-image`.

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Administrator
    participant Policy as AdminPolicy
    participant Controller as AdminProfileController
    participant Service as ProfileService
    participant Repo as ProfileRepository
    participant Validator as File/Image validation
    participant Replace as StorageReplacement
    participant Storage as Supabase Storage
    participant DB as PostgreSQL
    participant Log as Structured logger

    Admin->>Policy: multipart file + Bearer JWT
    Policy->>Controller: Authorized request
    Controller->>Service: ReplaceAvatarAsync or ReplaceHeroImageAsync
    Service->>Repo: GetForUpdateAsync()
    Repo->>DB: Load tracked Profile
    alt Profile chưa tồn tại
        Service-->>Admin: 404 Problem Details
    end

    Service->>Validator: Validate non-empty, size, extension, MIME and signature
    opt Hero image
        Service->>Validator: Validate configured width and height limits
    end
    alt Validation thất bại
        Validator-->>Admin: 400 or 413 Problem Details
    else File hợp lệ
        Service->>Service: Generate profiles/{profileId}/{uuid}.{extension}
        Service->>Replace: ReplaceAsync(new upload, old object key, persist callback)
        Replace->>Storage: Upload new object
        Storage-->>Replace: New bucket/object key
        Replace->>Service: Persist callback(new object)
        Service->>Repo: Save new object key
        Repo->>DB: Commit Profile media metadata

        alt Database persistence thất bại
            Replace->>Storage: Delete newly uploaded object
            opt Compensation delete cũng thất bại
                Replace->>Log: Reconciliation-required error
            end
            Replace-->>Admin: Preserve original persistence error
        else Metadata đã commit
            opt Có old object khác new object
                Replace->>Storage: Delete old object
            end
            Service->>Log: MediaReplaced(ProfileId, media kind)
            Service->>Storage: Resolve public URL
            Storage-->>Service: Public URL
            Service-->>Controller: Media response
            Controller-->>Admin: 200 ApiResponse
        end
    end
```

### Trạng thái khi lỗi xóa old object sau commit

Database đã trỏ đến new object trước khi old object bị xóa. Nếu thao tác xóa old object thất bại, metadata mới không rollback; request nhận lỗi Storage và old object có thể cần reconciliation/cleanup sau đó.

## 12. Xử lý lỗi tập trung

```mermaid
flowchart LR
    Failure[Exception from API, Application or Infrastructure]
    Handler[GlobalExceptionHandler]
    Log[Server-side structured log]
    Problem[application/problem+json with requestId]

    Failure --> Handler
    Handler -->|ValidationException| E400[400 Validation failed]
    Handler -->|AuthenticationFailedException| E401[401 Authentication failed]
    Handler -->|ForbiddenException| E403[403 Forbidden]
    Handler -->|NotFoundException| E404[404 Resource not found]
    Handler -->|ConflictException| E409[409 Conflict]
    Handler -->|PayloadTooLargeException| E413[413 Payload too large]
    Handler -->|ServiceUnavailableException| E503[503 Service unavailable]
    Handler -->|Unexpected exception| E500[500 generic server error]
    Handler --> Problem
    E500 --> Log
```

Chỉ validation error có `errors` theo field. Mọi Problem Details có `requestId`. Unexpected exception được log server-side nhưng response không chứa stack trace, SQL/provider detail hoặc secret.

## 13. Ma trận endpoint và data store

| Endpoint | Quyền | Service | Data store/adapter chính | Public disclosure |
| --- | --- | --- | --- | --- |
| `POST /api/v1/auth/login` | Anonymous | `AuthService` | ASP.NET Identity, JWT issuer | Chỉ token thành công; lỗi credential dùng thông báo chung |
| `POST /api/v1/auth/refresh` | Cookie + Origin + CSRF | `AuthService` | PostgreSQL session/token, JWT issuer | Rotate token; reuse revoke toàn session |
| `POST /api/v1/auth/logout` | Authenticated + CSRF | `AuthService` | PostgreSQL session/token | Idempotent, xóa refresh cookie |
| `POST /api/v1/auth/logout-all` | Authenticated + CSRF | `AuthService` | PostgreSQL session/token, `AspNetUsers` | Revoke mọi session, tăng auth version |
| `GET /api/v1/auth/sessions` | Authenticated | `AuthService` | PostgreSQL session | Chỉ session của current user, không trả token/hash |
| `DELETE /api/v1/auth/sessions/{sessionId}` | Authenticated + CSRF | `AuthService` | PostgreSQL session/token | Scope bằng current user, chống IDOR |
| `GET /api/v1/admin/dashboard` | Admin | `DashboardService` | PostgreSQL | Không public |
| `GET /api/v1/portfolio/{slug}/profile` | Anonymous | `ProfileService` | PostgreSQL, public Storage URL resolver | Exact locale, published links, visibility flags |
| `GET /api/v1/admin/profile` | Admin | `ProfileService` | PostgreSQL | Full admin DTO |
| `PUT /api/v1/admin/profile` | Admin | `ProfileService` | PostgreSQL | Không cho client ghi media URL |
| `POST /api/v1/admin/profile/avatar` | Admin | `ProfileService` | PostgreSQL + Supabase Storage | Trả public URL, không trả object key |
| `POST /api/v1/admin/profile/hero-image` | Admin | `ProfileService` | PostgreSQL + Supabase Storage | Trả public URL, không trả object key |
| `GET /api/v1/portfolio/{slug}/about` | Anonymous | `AboutService` | PostgreSQL | Chỉ Published, exact locale, conditional counters |
| `GET /api/v1/admin/about` | Admin | `AboutService` | PostgreSQL | Full admin DTO |
| `PUT /api/v1/admin/about` | Admin | `AboutService` | PostgreSQL | Publishing yêu cầu content `en` và `vi` |

## 14. Điểm cần lưu ý khi vận hành

- Swagger phản ánh endpoint thực tế và chỉ bật trong Development tại `/swagger`.
- `docs/api/API_CONTRACT.md` là hợp đồng cho toàn MVP, bao gồm cả endpoint của sprint tương lai; không dùng riêng file đó để suy luận rằng mọi endpoint đã được triển khai.
- Public images dùng bucket public-read/server-write. Storage service-role key chỉ tồn tại phía backend.
- Các cột database `profiles.avatar_url` và `profiles.hero_image_url` hiện lưu object key theo Storage contract; public URL được tạo khi map response.
- PostgreSQL integration tests cần Docker và image `postgres:17-alpine`.
