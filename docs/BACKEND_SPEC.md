# Đặc tả Backend — Personal Portfolio MVP

> Tài liệu mô tả phạm vi, kiến trúc và cách triển khai Backend cho website portfolio cá nhân. Backend sử dụng ASP.NET Core 10 Web API, Layered Architecture, PostgreSQL trên Supabase, Docker, GitHub, GitHub Actions và nền tảng triển khai miễn phí.

## 1. Mục tiêu

Backend chịu trách nhiệm:

- Cung cấp REST API cho nội dung portfolio công khai.
- Xác thực và phân quyền quản trị viên.
- Quản lý Hero/Profile, About, Skills, Experience, Projects và Certificates.
- Upload, quản lý phiên bản và cung cấp CV hiện hành.
- Nhận và quản lý contact message.
- Bảo vệ dữ liệu bằng validation, authorization, Row-Level Security và logging.
- Cho phép triển khai bằng Docker trên dịch vụ có free tier.

## 2. Phạm vi MVP

1. Hero và thông tin cá nhân.
2. About Me.
3. Skills.
4. Work Experience.
5. Projects.
6. Certificates.
7. Download CV.
8. Contact.
9. Admin CRUD cho nội dung.

Chat realtime, Redis, AI bot và group chat chưa triển khai trong phiên bản đầu.

## 3. Công nghệ sử dụng

| Công nghệ | Vai trò |
| --- | --- |
| .NET 10 | Runtime LTS |
| ASP.NET Core 10 Web API | REST API, middleware, DI, authentication và authorization |
| C# | Ngôn ngữ Backend |
| Entity Framework Core 10 | ORM, migration và truy vấn PostgreSQL |
| Npgsql | PostgreSQL provider cho EF Core |
| PostgreSQL | Cơ sở dữ liệu quan hệ |
| Supabase | PostgreSQL managed và Storage |
| FluentValidation hoặc DataAnnotations | Validation DTO |
| ProblemDetails | Chuẩn hóa error response |
| Swagger/OpenAPI | Tài liệu và kiểm thử API |
| Serilog | Structured logging |
| xUnit | Unit và integration test |
| Docker | Đóng gói ứng dụng và triển khai nhất quán |
| Git + GitHub | Quản lý source code và pull request |
| GitHub Actions | Build, test và build Docker image |
| Render/Koyeb free tier | Chạy Docker container cho project cá nhân |

> Gói miễn phí, giới hạn tài nguyên và cơ chế sleep có thể thay đổi. Cần kiểm tra lại chính sách của nhà cung cấp trước khi triển khai production.

## 4. Layered Architecture

Backend sử dụng **Layered Architecture đơn giản**, ưu tiên dễ hiểu, dễ test và triển khai nhanh cho portfolio MVP.

Backend chia thành ba project chính:

```text
Portfolio.sln
├── src/
│   ├── Portfolio.Api/
│   ├── Portfolio.Application/
│   └── Portfolio.Infrastructure/
└── tests/
    ├── Portfolio.UnitTests/
    └── Portfolio.IntegrationTests/
```

Luồng xử lý chuẩn:

```text
Controller
    ↓
IService
    ↓
Service
    ↓
IRepository
    ↓
Repository
    ↓
PortfolioDbContext
    ↓
PostgreSQL
```

Nguyên tắc thiết kế:

- Không dùng Clean Architecture đầy đủ cho MVP này.
- Không tạo `Portfolio.Domain` riêng ở giai đoạn hiện tại.
- Không dùng CQRS/MediatR/Domain Events nếu chưa có nhu cầu cụ thể.
- Dùng interface tại các boundary quan trọng để giữ Dependency Inversion rõ ràng.
- Dùng feature-specific repository; không dùng Generic Repository.
- Không tạo custom Unit of Work chỉ để wrap `DbContext.SaveChangesAsync()`.
- `PortfolioDbContext` là Unit of Work của EF Core.

### 4.1 Portfolio.Api

Chịu trách nhiệm:

- Controllers và API endpoints.
- Nhận request, model binding và trả response.
- Middleware.
- Authentication và authorization.
- Swagger/OpenAPI.
- Dependency Injection composition root.
- Global exception handling và ProblemDetails.
- Health checks.
- Cấu hình ứng dụng.

Controller:

- Chỉ xử lý HTTP concern.
- Inject `IService` tương ứng.
- Không chứa business rule.
- Không inject repository trực tiếp.
- Không truy cập `PortfolioDbContext`.
- Không viết EF Core query hoặc SQL trực tiếp.

Ví dụ:

```text
ProjectsController
    ↓
IProjectService
```

### 4.2 Portfolio.Application

Chịu trách nhiệm:

- Application services.
- Service interfaces (`IProjectService`, `IResumeService`, ...).
- Repository abstractions (`IProjectRepository`, `IResumeRepository`, ...).
- DTO/request/response models.
- Validation.
- Business/application rules.
- Mapping.
- Abstraction cho storage, current user, clock hoặc external service khi cần.

Service:

- Chứa business/application logic.
- Phụ thuộc vào abstraction, không phụ thuộc concrete repository.
- Có thể phối hợp nhiều repository và external-service abstraction trong một use case.
- Không chứa EF Core query hoặc SQL.

Ví dụ:

```text
IProjectService
    ↑
ProjectService
    ↓
IProjectRepository
```

Không tạo abstraction chỉ để tăng số layer.

### 4.3 Portfolio.Infrastructure

Chịu trách nhiệm:

- `PortfolioDbContext`.
- Entity Framework Core configurations.
- EF Core migrations.
- Repository implementations.
- PostgreSQL/Npgsql.
- Supabase Storage implementation.
- Authentication infrastructure.
- External service implementations.

Repository:

- Implement interface được Application định nghĩa.
- Được phép dùng `PortfolioDbContext` và EF Core.
- Chứa query/persistence logic.
- Không chứa business rule.

Ví dụ:

```text
IProjectRepository
    ↑ implements
ProjectRepository
    ↓
PortfolioDbContext
```

### 4.4 Entity và business model

MVP hiện tại không tạo project `Portfolio.Domain` riêng.

Entity/model có thể được tổ chức theo cấu trúc hiện có của project, nhưng phải tuân thủ:

- Entity không chứa HTTP concern.
- Không để Controller thao tác trực tiếp với persistence entity.
- Business rule quan trọng đặt ở Application service hoặc entity method khi rule gắn chặt với trạng thái entity.
- Không thêm Domain layer chỉ vì lý do lý thuyết.

Nếu sau này business domain đủ phức tạp, có thể tách `Portfolio.Domain` bằng một quyết định kiến trúc riêng.


## 5. Dependency rule

Dependency giữa các project:

```text
Portfolio.Api
    ├── references Portfolio.Application
    └── references Portfolio.Infrastructure
         (composition root / DI registration)

Portfolio.Infrastructure
    └── references Portfolio.Application
         để implement repository/service abstractions

Portfolio.Application
    └── không reference Portfolio.Infrastructure
```

Dependency Inversion:

```text
High-level business logic
ProjectService
    ↓
IProjectRepository
    ↑
ProjectRepository
Low-level persistence
```

Nguyên tắc:

- Controller phụ thuộc `IService`, không phụ thuộc concrete service.
- Service phụ thuộc `IRepository` hoặc external-service abstraction.
- Infrastructure implement các abstraction do Application định nghĩa.
- Application không reference Infrastructure.
- API là composition root, đăng ký implementation qua Dependency Injection.
- Không dùng service locator.
- Không tự `new` infrastructure dependency bên trong Controller hoặc Service.
- Repository không chứa business rule.
- Không bắt buộc tạo interface cho mọi class; chỉ dùng abstraction tại boundary có ý nghĩa.


## 6. Cấu trúc source đề xuất

```text
src/
├── Portfolio.Api/
│   ├── Controllers/
│   ├── Middleware/
│   ├── Filters/
│   ├── Extensions/
│   ├── Authorization/
│   ├── OpenApi/
│   ├── Program.cs
│   └── appsettings.json
│
├── Portfolio.Application/
│   ├── Common/
│   │   ├── Exceptions/
│   │   ├── Models/
│   │   └── Abstractions/
│   │       ├── Storage/
│   │       ├── Authentication/
│   │       └── Time/
│   │
│   ├── Profiles/
│   │   ├── IProfileService.cs
│   │   ├── ProfileService.cs
│   │   ├── IProfileRepository.cs
│   │   ├── Dtos/
│   │   └── Validators/
│   │
│   ├── Skills/
│   ├── Experiences/
│   ├── Projects/
│   │   ├── IProjectService.cs
│   │   ├── ProjectService.cs
│   │   ├── IProjectRepository.cs
│   │   ├── Dtos/
│   │   └── Validators/
│   │
│   ├── Certificates/
│   ├── Resumes/
│   └── Contacts/
│
└── Portfolio.Infrastructure/
    ├── Persistence/
    │   ├── PortfolioDbContext.cs
    │   ├── Entities/
    │   ├── Configurations/
    │   ├── Repositories/
    │   │   ├── ProjectRepository.cs
    │   │   ├── ResumeRepository.cs
    │   │   └── ...
    │   └── Migrations/
    ├── Storage/
    ├── Authentication/
    └── DependencyInjection.cs
```

Tổ chức theo feature ở `Portfolio.Application` để Controller/Service/Repository abstraction của cùng một domain chức năng dễ tìm.

Không tạo `IRepository<T>` dùng chung cho toàn hệ thống.

Repository phải có API phản ánh đúng query/use case của feature, ví dụ:

```csharp
public interface IProjectRepository
{
    Task<bool> SlugExistsAsync(
        string slug,
        CancellationToken cancellationToken);

    Task<Project?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<Project?> GetPublishedBySlugAsync(
        string slug,
        string locale,
        CancellationToken cancellationToken);

    Task AddAsync(
        Project project,
        CancellationToken cancellationToken);
}
```


## 7. API conventions

### 7.1 Base URL

```text
/api/v1
```

### 7.2 Response thành công

```json
{
  "data": {},
  "message": "Success",
  "meta": null
}
```

Với collection phân trang:

```json
{
  "data": [],
  "meta": {
    "page": 1,
    "pageSize": 20,
    "total": 42,
    "totalPages": 3
  }
}
```

### 7.3 Error response

Sử dụng RFC Problem Details:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/v1/contact",
  "requestId": "00-...",
  "errors": {
    "email": ["Email is invalid."]
  }
}
```

### 7.4 HTTP status code

| Status | Sử dụng |
| --- | --- |
| 200 | GET/PUT/PATCH thành công |
| 201 | Tạo mới thành công |
| 204 | Xóa thành công, không có response body |
| 400 | Request hoặc business rule không hợp lệ |
| 401 | Chưa xác thực/token không hợp lệ |
| 403 | Đã xác thực nhưng không đủ quyền |
| 404 | Không tìm thấy resource |
| 409 | Trùng slug, xung đột phiên bản hoặc dữ liệu |
| 413 | File/request quá lớn |
| 429 | Vượt rate limit |
| 500 | Lỗi không dự kiến |

## 8. Authentication và authorization

### 8.1 ASP.NET Core Identity

- Backend xác thực tài khoản Admin bằng ASP.NET Core Identity qua `POST /api/v1/auth/login`.
- Backend phát access JWT RS256 thời hạn mặc định 10 phút và xác minh signature, thuật toán, `kid`, issuer, audience, type, role, session, auth version và thời gian hết hạn.
- Refresh token là opaque credential xoay vòng, chỉ đi qua cookie `__Host-refresh`; database chỉ lưu HMAC-SHA256 của secret.
- `sub` trong token là ID của `AspNetUsers`; role `Admin` do Identity quản lý phía server.
- MVP không có public signup và không dùng Supabase Auth cho tài khoản Admin.
- Quyền admin phải được đọc/kiểm tra phía server; không tin cờ do client gửi.

### 8.2 Policies

```text
PublicReadPolicy
AdminPolicy
```

- Public endpoint chỉ trả bản ghi `is_published = true`.
- Admin endpoint bắt buộc `AdminPolicy`.
- MVP chỉ có một Portfolio và schema content không có owner FK. Nếu sau này hỗ trợ nhiều Portfolio, phải có migration và quyết định kiến trúc riêng trước khi thêm ownership policy.

### 8.3 Lưu token

- Không log access token.
- Nếu dùng cookie, đặt `HttpOnly`, `Secure` và `SameSite` phù hợp; bổ sung CSRF protection.
- Nếu dùng Authorization header, Frontend gửi `Bearer <token>` qua HTTPS.

## 9. Database PostgreSQL trên Supabase

### 9.1 Bảng chính

| Bảng | Mục đích |
| --- | --- |
| `profiles`, `profile_translations`, `social_links` | Hồ sơ portfolio và liên kết |
| `abouts`, `about_translations` | About Me |
| `skill_categories` | Nhóm kỹ năng |
| `technologies` | Kỹ năng/công nghệ |
| `work_experiences` | Kinh nghiệm làm việc |
| `projects` | Dự án |
| `project_images` | Gallery dự án |
| `project_technologies` | Công nghệ của dự án |
| `certificates` | Chứng chỉ |
| `resumes`, `resume_version_counters` | Các phiên bản CV và bộ đếm version |
| `contact_messages` | Lời nhắn liên hệ |
| `site_settings` | Cấu hình site dạng JSON |

### 9.2 Trường dùng chung cho content

```text
id              uuid primary key
is_published    boolean not null default false
display_order   integer not null default 0
created_at      timestamptz not null
updated_at      timestamptz not null
```

Không phải bảng nào cũng cần đủ các trường trên. `contact_messages` không phải nội dung public và không cần `is_published`.

### 9.3 Index đề xuất

- Partial index theo `is_published` và `display_order` cho danh sách public.
- Unique `slug` toàn cục cho Profile và Project trong MVP một Portfolio.
- `(status, created_at desc)` cho contact admin.
- Partial unique index cho CV current theo `language_code` với điều kiện `is_active = true`.
- Index các foreign key như `project_images.project_id` và `skills.category_id`.

### 9.4 Database access và RLS

- API dùng kết nối Npgsql server-side và là đường truy cập duy nhất tới bảng ứng dụng.
- Supabase Data API không cấp quyền anonymous/authenticated cho schema ứng dụng của MVP.
- Production dùng database role riêng cho API; migration dùng credential riêng và chạy qua một runner được kiểm soát.
- JWT của ASP.NET Core Identity không được xem là Supabase Auth JWT và không ánh xạ giả sang `auth.uid()`.
- Service-role key không bao giờ được đưa xuống Frontend.
- Storage bucket policy chỉ cho phép public read ở bucket public đã duyệt; mọi write/delete và signed URL dùng Backend.

Nếu bổ sung table RLS sau này, policy và production EF role phải được integration test cùng nhau trước deployment. Chi tiết quyết định nằm trong `docs/adr/0001-mvp-contract-decisions.md`.

## 10. Đặc tả chức năng Backend

### BE-01 — Hero và Profile

#### Endpoint

| Method | Endpoint | Quyền | Chức năng |
| --- | --- | --- | --- |
| GET | `/api/v1/portfolio/{slug}/profile` | Public | Hồ sơ công khai |
| PUT | `/api/v1/admin/profile` | Admin | Cập nhật hồ sơ |
| POST | `/api/v1/admin/profile/avatar` | Admin | Upload avatar |
| POST | `/api/v1/admin/profile/hero-image` | Admin | Upload Hero image |

#### Quy tắc nghiệp vụ

- `name` và `title` bắt buộc.
- Chỉ trả email/phone khi cấu hình công khai.
- `shortBio` có giới hạn độ dài.
- Avatar phải đúng MIME và dung lượng cho phép.
- MVP không dùng application cache. Nếu cache được phê duyệt sau này, update Profile phải vô hiệu hóa cache public tương ứng.

### BE-02 — About Me

| Method | Endpoint | Quyền | Chức năng |
| --- | --- | --- | --- |
| GET | `/api/v1/portfolio/{slug}/about` | Public | About đã publish |
| PUT | `/api/v1/admin/about` | Admin | Cập nhật About |

Quy tắc:

- Không trả draft qua public API.
- MVP lưu và trả plain text. Nếu Markdown/rich text được phê duyệt sau này, nội dung phải được sanitize theo contract riêng.
- `yearsOfExperience` không âm.
- About được lưu trong `abouts` và `about_translations` theo schema hiện tại; thay đổi versioning cần ADR/migration riêng.

### BE-03 — Skills

| Method | Endpoint | Quyền | Chức năng |
| --- | --- | --- | --- |
| GET | `/api/v1/portfolio/{slug}/skills` | Public | Kỹ năng theo nhóm |
| POST | `/api/v1/admin/skill-categories` | Admin | Tạo nhóm |
| PUT | `/api/v1/admin/skill-categories/{id}` | Admin | Sửa nhóm |
| DELETE | `/api/v1/admin/skill-categories/{id}` | Admin | Xóa nhóm |
| POST | `/api/v1/admin/skills` | Admin | Tạo kỹ năng |
| PUT | `/api/v1/admin/skills/{id}` | Admin | Sửa kỹ năng |
| DELETE | `/api/v1/admin/skills/{id}` | Admin | Xóa kỹ năng |
| PATCH | `/api/v1/admin/skills/reorder` | Admin | Sắp thứ tự |

Quy tắc:

- Skill phải thuộc category tồn tại trong cùng Portfolio.
- Không trùng tên trong cùng category.
- Level thuộc enum `Primary`, `Experienced`, `Familiar`, `Learning`.
- `icon` là object bắt buộc có dạng `{ "type": "lucide|image|text", "value": "..." }`; public DTO và admin DTO dùng cùng contract này.
- Với `type = text`, `value` sau khi trim dài từ 1 đến 6 ký tự để không phá vỡ layout.
- Với `type = lucide`, `value` là tên kebab-case nằm trong allowlist do Backend và Frontend thống nhất; không nhận SVG/HTML do client gửi.
- Với `type = image`, `value` là đường dẫn storage do hệ thống quản lý hoặc HTTPS URL thuộc host được phép. Chỉ nhận SVG, PNG, JPEG hoặc WebP và phải kiểm tra MIME, kích thước, tên file.
- Backend lưu icon bằng hai cột `icon_type`, `icon_value`; không serialize object JSON vào một column để có thể tạo constraint và truy vấn rõ ràng.
- Không xóa category đang có skill, trừ khi request chỉ rõ cách xử lý.
- Reorder phải thực hiện trong transaction.

Contract mẫu:

```json
{
  "name": { "en": "Supabase", "vi": "Supabase" },
  "level": "Primary",
  "categoryId": "d1e6b31e-5907-4db9-874a-c0bdd13e74f4",
  "icon": {
    "type": "image",
    "value": "/images/skills/supabase.svg"
  },
  "isPublished": true
}
```

Entity/DTO mapping:

```text
SkillIconDto.Type   <-> technologies.icon_type
SkillIconDto.Value  <-> technologies.icon_value
```

Nếu triển khai upload file cho icon, dùng endpoint riêng `POST /api/v1/admin/skills/{id}/icon` dạng multipart. Backend upload file mới, cập nhật `icon_type = image` và `icon_value` trong transaction nghiệp vụ, sau đó mới xóa object cũ. Client không được gửi đường dẫn filesystem hoặc storage key tùy ý.

### BE-04 — Work Experience

| Method | Endpoint | Quyền | Chức năng |
| --- | --- | --- | --- |
| GET | `/api/v1/portfolio/{slug}/experiences` | Public | Danh sách published |
| POST | `/api/v1/admin/experiences` | Admin | Tạo |
| PUT | `/api/v1/admin/experiences/{id}` | Admin | Sửa |
| DELETE | `/api/v1/admin/experiences/{id}` | Admin | Xóa |
| PATCH | `/api/v1/admin/experiences/reorder` | Admin | Sắp thứ tự |

Quy tắc:

- Company, position và start date bắt buộc.
- Nếu `isCurrent = true`, `endDate` phải null.
- Nếu không current, `endDate` phải lớn hơn hoặc bằng `startDate`.
- Public API sắp xếp theo `displayOrder`, sau đó `startDate desc`.

### BE-05 — Projects

| Method | Endpoint | Quyền | Chức năng |
| --- | --- | --- | --- |
| GET | `/api/v1/portfolio/{slug}/projects` | Public | Danh sách project |
| GET | `/api/v1/portfolio/{slug}/projects/{projectSlug}` | Public | Chi tiết project |
| POST | `/api/v1/admin/projects` | Admin | Tạo |
| PUT | `/api/v1/admin/projects/{id}` | Admin | Sửa |
| DELETE | `/api/v1/admin/projects/{id}` | Admin | Xóa |
| POST | `/api/v1/admin/projects/{id}/images` | Admin | Upload gallery |
| PATCH | `/api/v1/admin/projects/{id}/publish` | Admin | Publish/unpublish |
| PATCH | `/api/v1/admin/projects/reorder` | Admin | Sắp thứ tự |

Quy tắc:

- Slug duy nhất trong phạm vi user.
- Public endpoint không trả draft.
- Kiểm tra URL demo/repository.
- Validate MIME thực tế, extension và kích thước ảnh.
- Tạo storage key phía server, không dùng nguyên tên file từ client.
- Chỉ xóa file cũ sau khi file mới upload và database update thành công.

### BE-06 — Certificates

| Method | Endpoint | Quyền | Chức năng |
| --- | --- | --- | --- |
| GET | `/api/v1/portfolio/{slug}/certificates` | Public | Danh sách chứng chỉ |
| POST | `/api/v1/admin/certificates` | Admin | Tạo |
| PUT | `/api/v1/admin/certificates/{id}` | Admin | Sửa |
| DELETE | `/api/v1/admin/certificates/{id}` | Admin | Xóa |
| POST | `/api/v1/admin/certificates/{id}/file` | Admin | Upload minh chứng |

Quy tắc:

- Name, issuing organization và issue date bắt buộc.
- Expiration date phải sau issue date.
- Trạng thái expired được tính từ ngày hết hạn, không cần lưu dư thừa.
- Credential ID chỉ được trả khi cho phép công khai.
- File chỉ chấp nhận ảnh hoặc PDF theo cấu hình.

### BE-07 — CV

| Method | Endpoint | Quyền | Chức năng |
| --- | --- | --- | --- |
| GET | `/api/v1/portfolio/{slug}/cv/current?language=en` | Public | CV current |
| GET | `/api/v1/admin/cv` | Admin | Danh sách phiên bản |
| POST | `/api/v1/admin/cv` | Admin | Upload CV |
| PATCH | `/api/v1/admin/cv/{id}/current` | Admin | Đặt current |
| DELETE | `/api/v1/admin/cv/{id}` | Admin | Xóa phiên bản |

Quy tắc:

- Khuyến nghị public CV chỉ dùng PDF.
- Mỗi user/ngôn ngữ chỉ có một bản current.
- Thao tác đổi current phải nằm trong transaction.
- Upload thất bại không được làm mất CV current cũ.
- Không xóa CV current nếu chưa chỉ định bản thay thế.
- Public API trả signed URL ngắn hạn hoặc stream file.

### BE-08 — Contact

| Method | Endpoint | Quyền | Chức năng |
| --- | --- | --- | --- |
| POST | `/api/v1/contact` | Public | Gửi liên hệ |
| GET | `/api/v1/admin/contacts` | Admin | Danh sách |
| GET | `/api/v1/admin/contacts/{id}` | Admin | Chi tiết |
| PATCH | `/api/v1/admin/contacts/{id}/status` | Admin | Đổi trạng thái |
| DELETE | `/api/v1/admin/contacts/{id}` | Admin | Xóa |

Quy tắc:

- Validate name, email, subject và message.
- Giới hạn độ dài từng trường.
- Rate limit theo dữ liệu kết nối đáng tin cậy.
- Dùng honeypot hoặc Cloudflare Turnstile nếu spam tăng.
- Public chỉ được tạo, không được đọc contact message.
- Notification email là side effect; lỗi gửi email không được làm mất message đã lưu.

### BE-09 — Admin CRUD

Yêu cầu:

- Mọi endpoint `/admin` yêu cầu `AdminPolicy`.
- CRUD dựa trên `AdminPolicy` trong MVP một Portfolio; API không giả lập ownership khi schema chưa có owner FK.
- Hỗ trợ pagination, search và filter trạng thái.
- Hỗ trợ publish/unpublish và reorder.
- Ghi structured audit event qua Serilog cho create, update, delete, publish và thao tác file quan trọng; durable audit table nằm ngoài MVP.
- Delete có thể là soft delete với resource cần khôi phục.
- API không nhận `userId` hoặc `isAdmin` từ client để quyết định quyền.

## 11. Entity cốt lõi

MVP không tạo project `Portfolio.Domain` riêng.

Entity persistence được quản lý bởi EF Core và đặt trong Infrastructure hoặc vị trí thống nhất theo project convention.

Ví dụ entity dùng chung cho content:

```csharp
public abstract class ContentEntity
{
    public Guid Id { get; protected set; }
    public bool IsPublished { get; protected set; }
    public int DisplayOrder { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected set; }
    public DateTimeOffset UpdatedAt { get; protected set; }
}
```

Nguyên tắc:

- Không expose setter public tùy ý cho state quan trọng nếu có thể làm mất invariant.
- Rule thuần business có thể đặt trong method của entity khi hợp lý.
- Rule phụ thuộc use case, repository, current user hoặc external service đặt ở Application service.
- Không tạo Domain layer riêng chỉ để chứa entity/anemic model.
- Public API không trả EF entity trực tiếp; dùng response DTO/projection.


## 12. Service, Repository và EF Core Unit of Work

### 12.1 Service abstraction

Controller phụ thuộc service abstraction:

```csharp
public interface IProjectService
{
    Task<ProjectResponse> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken);
}
```

Implementation:

```csharp
public sealed class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;

    public ProjectService(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }
}
```

Mục tiêu:

- Controller không phụ thuộc concrete service.
- Dễ unit test Controller.
- Giữ boundary giữa HTTP layer và Application layer rõ ràng.

### 12.2 Feature-specific Repository

Application định nghĩa repository interface theo nhu cầu của feature:

```csharp
public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<Project?> GetPublishedBySlugAsync(
        string slug,
        string locale,
        CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(
        string slug,
        CancellationToken cancellationToken);

    Task AddAsync(
        Project project,
        CancellationToken cancellationToken);

    void Remove(Project project);
}
```

Infrastructure implement interface bằng EF Core:

```text
IProjectRepository
    ↑
ProjectRepository
    ↓
PortfolioDbContext
    ↓
PostgreSQL
```

Không dùng Generic Repository kiểu:

```csharp
IRepository<T>
```

chỉ để wrap lại `DbSet<T>`.

### 12.3 Không tạo custom Unit of Work

Không tạo:

```csharp
IUnitOfWork
UnitOfWork
```

chỉ để wrap:

```csharp
DbContext.SaveChangesAsync()
```

`PortfolioDbContext` đã đảm nhiệm Unit of Work của EF Core:

- Change Tracking.
- Theo dõi nhiều entity trong cùng scope.
- `SaveChangesAsync()`.
- Transaction API.

Khi một use case cần atomicity, dùng cùng scoped `PortfolioDbContext` và EF Core transaction.

Ví dụ:

```csharp
await using var transaction =
    await dbContext.Database.BeginTransactionAsync(cancellationToken);

try
{
    // multiple related changes

    await dbContext.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
}
catch
{
    await transaction.RollbackAsync(cancellationToken);
    throw;
}
```

Không tạo abstraction mới nếu nó chỉ forward trực tiếp sang EF Core mà không tạo thêm giá trị.

### 12.4 SaveChanges và transaction boundary

Nguyên tắc:

- Không gọi `SaveChangesAsync()` rải rác không cần thiết trong một use case.
- Các thay đổi phải atomic dùng cùng DbContext/transaction.
- Reorder, Resume version allocation, active Resume switch và multi-row update phải đánh giá transaction.
- Không giữ DB transaction mở trong lúc thực hiện network call chậm nếu không có thiết kế failure/compensation rõ ràng.
- Truyền `CancellationToken` xuyên suốt:

```text
Controller
→ Service
→ Repository
→ EF Core / Storage
```

### 12.5 Testing boundary

Unit test Application service:

```text
Service
→ mock IRepository
```

Integration test persistence:

```text
Repository
→ PortfolioDbContext
→ PostgreSQL
```

AAA được áp dụng cho unit test:

```text
Arrange
Act
Assert
```

Mock repository không được dùng làm bằng chứng rằng EF Core query hoặc PostgreSQL constraint hoạt động đúng.


## 13. Validation

Validation chia thành ba mức:

1. DTO validation: required, length, format và range.
2. Application rule: slug trùng, authorization/single-Portfolio scope và trạng thái publish.
3. Entity/database invariant: khoảng ngày hợp lệ, unique/constraint và các invariant cần luôn đúng.

Không chỉ dựa vào Frontend validation. Mọi input phải được kiểm tra lại ở Backend.

## 14. Global exception handling

Đăng ký:

```csharp
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

app.UseExceptionHandler();
```

Handler ánh xạ:

- Validation exception → 400.
- Not found exception → 404.
- Conflict exception → 409.
- Forbidden exception → 403.
- Exception không xác định → 500 và không trả stack trace cho client.

Response cần có `requestId` để đối chiếu log.

## 15. Upload file và Supabase Storage

### Buckets đề xuất

```text
avatars
project-images
certificate-files
cv-files
```

### Quy trình upload

1. Kiểm tra file tồn tại và dung lượng lớn hơn 0.
2. Kiểm tra giới hạn dung lượng.
3. Kiểm tra extension và MIME thực tế.
4. Sinh storage key bằng UUID.
5. Upload file vào bucket.
6. Lưu metadata vào PostgreSQL.
7. Nếu database thất bại, cố gắng xóa file vừa upload.
8. Nếu thay file, chỉ xóa file cũ sau khi dữ liệu mới đã lưu thành công.

Không dùng `IFormFile.FileName` làm tên lưu trữ. Tên gốc chỉ dùng làm metadata sau khi sanitize.

## 16. Logging và monitoring

- Dùng structured logging với Serilog.
- Log request ID, method, path, status code và duration.
- Không log password, JWT, connection string hoặc nội dung file.
- Log warning cho validation bất thường và rate limit.
- Log error kèm exception ở server; client chỉ nhận thông báo an toàn.
- Cung cấp `/health` và `/health/ready` nếu nền tảng deploy hỗ trợ health check.

## 17. CORS

Production chỉ cho phép domain Frontend:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(configuration["Frontend:Origin"]!)
              .AllowAnyHeader()
              .AllowAnyMethod());
});
```

Không dùng `AllowAnyOrigin()` cùng credentials. Preview domain chỉ thêm khi thực sự cần và phải kiểm soát rõ.

## 18. Configuration và secret

```text
ConnectionStrings__PostgreSql
Frontend__Origin
Jwt__Issuer
Jwt__Audience
Jwt__ActiveKeyId
Jwt__SigningCertificatePath
Jwt__SigningCertificatePassword
Jwt__ValidationCertificatePaths__<kid>
Jwt__AccessTokenMinutes
Jwt__ClockSkewSeconds
RefreshToken__IdleLifetimeDays
RefreshToken__AbsoluteLifetimeDays
RefreshToken__Pepper
RefreshToken__CookieName
RefreshToken__CsrfCookieName
RefreshToken__CookieSameSite
BootstrapAdmin__Enabled
BootstrapAdmin__Email
BootstrapAdmin__Password
SupabaseStorage__Url
SupabaseStorage__ServiceRoleKey
Upload__MaxFileSize
RateLimit__Auth__LoginPermitLimit
RateLimit__Auth__RefreshPermitLimit
RateLimit__Auth__WindowSeconds
RateLimit__Contact__PermitLimit
```

Quy tắc:

- Local development dùng User Secrets hoặc `.env` không commit.
- Production secret lưu trong dashboard của nền tảng deploy.
- Repository chỉ có `appsettings.json` không chứa secret và file `.env.example`.
- Validate configuration khi application khởi động.

## 19. Docker

Dockerfile multi-stage:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish src/Portfolio.Api/Portfolio.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Portfolio.Api.dll"]
```

Thêm `.dockerignore`:

```text
**/bin
**/obj
.git
.github
.vs
.vscode
TestResults
```

Yêu cầu container:

- Không ghi file upload vào filesystem tạm của container; dùng Supabase Storage.
- Lắng nghe port do nền tảng yêu cầu.
- Không chạy migration đồng thời từ nhiều instance nếu chưa có lock/strategy.
- Chạy bằng non-root user nếu base image và môi trường cho phép.

## 20. Git và GitHub

### Branch

- `main`: production-ready.
- `develop`: integration nếu cần.
- `feature/<name>`: chức năng.
- `fix/<name>`: sửa lỗi.

### Pull request checklist

- Migration được review.
- Không chứa secret.
- Unit/integration test thành công.
- Swagger/API contract được cập nhật.
- Có hướng dẫn rollback nếu thay đổi database quan trọng.

## 21. GitHub Actions

Workflow CI cơ bản:

```yaml
name: backend-ci

on:
  push:
    branches: [main]
  pull_request:

jobs:
  verify:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: dotnet restore
      - run: dotnet build --configuration Release --no-restore
      - run: dotnet test --configuration Release --no-build
      - run: docker build -t portfolio-api:${{ github.sha }} .
```

Nếu dùng GitHub Container Registry:

- Chỉ push image khi merge vào `main` hoặc tạo release tag.
- Gắn tag bằng commit SHA; không chỉ dùng `latest`.
- Dùng quyền tối thiểu cho `GITHUB_TOKEN`.

## 22. Deploy miễn phí

### Phương án đề xuất

| Thành phần | Dịch vụ |
| --- | --- |
| PostgreSQL | Supabase Free |
| Auth | ASP.NET Core Identity trong API/PostgreSQL |
| File Storage | Supabase Storage Free |
| ASP.NET Core API | Render hoặc Koyeb free tier bằng Docker |
| Container image | GitHub Container Registry |

### Quy trình triển khai Backend

1. Push source lên GitHub.
2. GitHub Actions build và test.
3. Build Docker image.
4. Nền tảng deploy build từ repository hoặc pull image.
5. Khai báo secrets trong dashboard.
6. Cấu hình health check `/health`.
7. Cấu hình domain/API URL cho Frontend.
8. Kiểm tra Swagger, database connection, upload và CORS.

### Hạn chế free tier cần dự phòng

- Instance có thể sleep khi không có traffic, làm request đầu tiên chậm.
- RAM/CPU thấp; tránh khởi động nặng và query không có index.
- Có giới hạn bandwidth, database size và storage.
- Không dựa vào local disk vì container có thể bị recreate.
- Portfolio cá nhân phù hợp free tier; khi traffic tăng cần nâng gói hoặc đổi nền tảng.

## 23. Database migration

Quy trình đề xuất:

```bash
dotnet ef migrations add AddProjects \
  --project src/Portfolio.Infrastructure \
  --startup-project src/Portfolio.Api

dotnet ef database update \
  --project src/Portfolio.Infrastructure \
  --startup-project src/Portfolio.Api
```

Production:

- Backup trước migration phá vỡ cấu trúc.
- Ưu tiên migration backward-compatible.
- Không tự động chạy migration từ mọi instance khi scale ngang.
- Giữ script rollback hoặc kế hoạch phục hồi cho thay đổi quan trọng.

## 24. Testing

| Loại test | Phạm vi |
| --- | --- |
| Unit test | Validator, Application Service, business rule, mapping/pure logic |
| Integration test | Repository, EF Core/PostgreSQL, transaction, auth và storage adapter |
| API test | Status code, ProblemDetails, authorization và serialization |
| Security test | Ownership, IDOR, file validation, CORS và rate limit |
| Regression test | Bug đã sửa và prevention rule trong `docs/BUG_LESSONS.md` |

### 24.1 Unit test

Unit test ưu tiên AAA:

```text
Arrange
Act
Assert
```

Application Service được test bằng mock abstraction:

```text
ProjectService
    ↓
Mock<IProjectRepository>
```

Unit test tập trung vào:

- business rule;
- validation;
- branching;
- mapping;
- authorization/single-Portfolio scope decision;
- publish logic;
- error mapping ở Application level.

Không mock EF Core query phức tạp để giả lập behavior của PostgreSQL.

### 24.2 Integration test

Repository và persistence behavior phải được test với EF Core/PostgreSQL thực tế hoặc môi trường integration tương đương.

Integration test tập trung vào:

- query translation;
- unique constraint;
- FK/check constraint;
- transaction;
- concurrency-sensitive behavior;
- migration;
- repository projection.

### 24.3 Regression test và bug lessons

Khi sửa bug có tính tái diễn:

1. Xác nhận root cause.
2. Sửa root cause.
3. Thêm regression test nếu thực tế.
4. Ghi prevention rule vào `docs/BUG_LESSONS.md` bằng skill `cg03-bug-lessons`.
5. `cg03-backend-feature` và `cg03-code-review` phải tham khảo bug lesson liên quan khi implement/review feature tương ứng.

Không đưa toàn bộ bug history vào `AGENTS.md`.

Các test tối thiểu:

1. Public API không trả draft.
2. User không phải admin nhận 403 khi gọi admin API.
3. Non-admin nhận 403 và không thể sửa/xóa Portfolio Content.
4. Không tạo experience có end date trước start date.
5. Chỉ một CV current cho mỗi user/ngôn ngữ.
6. Upload không hợp lệ không tạo database record.
7. Contact bị rate limit khi vượt ngưỡng.
8. Project slug trùng trả 409.
9. Các bug đã được ghi nhận có regression test tương ứng khi phù hợp.


## 25. Definition of Done

Một chức năng Backend được xem là hoàn thành khi:

- Endpoint và response đúng API contract.
- Controller chỉ phụ thuộc `IService` và không chứa business logic.
- Application Service phụ thuộc abstraction, không phụ thuộc concrete Repository.
- Persistence dùng feature-specific Repository.
- Không thêm Generic Repository.
- Không thêm custom Unit of Work chỉ để wrap EF Core.
- DTO validation và business rule đầy đủ.
- Authentication, authorization và giả định single-Portfolio được kiểm tra.
- Migration, index và RLS policy đã được review khi có liên quan.
- Transaction boundary được review cho multi-step write.
- Có logging và ProblemDetails thống nhất.
- Có unit/integration test cho happy path và lỗi chính.
- Bug fix có regression test khi phù hợp.
- Relevant prevention rules trong `docs/BUG_LESSONS.md` đã được kiểm tra.
- Swagger/OpenAPI được cập nhật.
- `dotnet build` thành công.
- `dotnet test` thành công.
- Docker image build thành công khi thay đổi deployment/container.
- GitHub Actions thành công trước merge/release.
- Deploy hoạt động và health check tốt khi release production.


## 26. Thứ tự triển khai đề xuất

1. Khởi tạo solution và ba project: `Portfolio.Api`, `Portfolio.Application`, `Portfolio.Infrastructure`.
2. Thiết lập project references và DI theo dependency rule.
3. Cấu hình PostgreSQL, EF Core, `PortfolioDbContext`, entity configuration và migration.
4. Tạo feature-specific repository abstraction/implementation đầu tiên để xác nhận pattern.
5. Cấu hình exception handler, ProblemDetails, logging và Swagger.
6. Chốt và cấu hình authentication + `AdminPolicy`.
7. Triển khai Profile và About theo flow:

```text
Controller
→ IService
→ Service
→ IRepository
→ Repository
→ PortfolioDbContext
```

8. Triển khai Skills và Work Experience.
9. Triển khai Projects và upload gallery.
10. Triển khai Certificates và CV versioning.
11. Triển khai Contact và rate limiting.
12. Bổ sung unit test, integration test và regression test.
13. Duy trì `docs/BUG_LESSONS.md` cho các bug có lesson tái sử dụng.
14. Bổ sung Docker và GitHub Actions.
15. Deploy Backend và cấu hình kết nối với Frontend.
