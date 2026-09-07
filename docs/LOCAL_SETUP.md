# Thiết lập backend trên máy local

Luồng setup/bootstrap, đăng nhập và xử lý API Profile/About được minh họa trong [Current Process Flows](architecture/CURRENT_PROCESS_FLOWS.md). Member mới nên đọc thêm [Backend Onboarding Guide](architecture/NEW_MEMBER_BACKEND_GUIDE.md) và [Authentication Guide](security/AUTHENTICATION_GUIDE.md).

Tài liệu này dành cho member mới bắt đầu từ lúc clone repository đến khi API kết nối được PostgreSQL và Storage trên Supabase.

## 1. Yêu cầu môi trường

Cài đặt:

- Git;
- PowerShell 5.1 trở lên;
- .NET SDK `10.0.400` hoặc patch mới hơn của .NET 10 theo `global.json`;
- Docker Desktop nếu chạy PostgreSQL integration tests;
- quyền truy cập Supabase project của team.

Kiểm tra SDK:

```powershell
dotnet --version
```

Clone và mở repository:

```powershell
git clone <repository-url>
Set-Location CG03_AboutMe
```

## 2. Chuẩn bị Supabase

### PostgreSQL

Trong Supabase Dashboard, mở project rồi chọn **Connect**.

Dùng một trong hai kết nối:

- **Direct connection**, port `5432`, nếu máy hỗ trợ IPv6;
- **Session pooler**, port `5432`, nếu mạng chỉ hỗ trợ IPv4.

Không dùng Transaction pooler port `6543` cho EF Core migration. Copy chính xác host và username từ Connect dialog. Session pooler thường có username dạng `postgres.<project-ref>`; direct connection thường dùng `postgres`.

Member cần chuẩn bị PostgreSQL host, port, database, username và database password.

### Storage

Trong Supabase Dashboard, mở **Storage** và tạo chính xác các bucket:

| Bucket | Visibility |
| --- | --- |
| `avatars` | Public |
| `project-images` | Public |
| `certificate-files` | Private |
| `cv-files` | Private |

API là nơi thực hiện write/delete và tạo signed URL. Không cấp service credential cho frontend.

Storage adapter hiện tại sử dụng legacy `service_role` JWT qua cả `apikey` và `Authorization: Bearer`. Lấy key này trong **Settings → API Keys → Legacy API Keys → service_role**. Không dùng `anon`, publishable key hoặc key `sb_secret_...` với adapter hiện tại.

## 3. Chạy setup tự động

Từ repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Setup-Local.ps1 -SyncUserSecrets
```

Nếu `.env` chưa tồn tại, script sẽ hỏi:

- Supabase PostgreSQL host, port, database, username và password;
- frontend origin local;
- Supabase project URL;
- legacy Storage `service_role` key.

Script tự sinh RSA development signing certificate, certificate password và refresh-token pepper bằng cryptographic randomness, sau đó:

1. tạo `.env` bằng UTF-8 không BOM;
2. kiểm tra cấu hình mà không in secret;
3. nạp cấu hình vào process chạy setup;
4. đồng bộ sang .NET User Secrets khi có `-SyncUserSecrets`;
5. restore local `dotnet-ef` tool và NuGet packages;
6. build solution ở Release;
7. liệt kê migration đang có;
8. không tự động sửa database.

`.env` đã được `.gitignore`. Script không overwrite file `.env` đang tồn tại.

### Các tùy chọn

| Tùy chọn | Tác dụng |
| --- | --- |
| `-EnvironmentFile <path>` | Dùng file env khác nằm bên trong repository |
| `-ValidateOnly` | Chỉ validate và nạp cấu hình, không restore/build/migrate |
| `-NonInteractive` | Không hiển thị prompt; fail nếu file không tồn tại hoặc không hợp lệ |
| `-SyncUserSecrets` | Đồng bộ `.env` vào User Secrets để IDE/IIS Express đọc được |
| `-ApplyMigrations` | Chủ động áp dụng EF migrations vào database đang cấu hình |

Kiểm tra nhanh file hiện có:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Setup-Local.ps1 -ValidateOnly -NonInteractive
```

## 4. Định dạng `.env`

ASP.NET Core không tự đọc `.env`; setup script là thành phần đọc file và truyền giá trị cho các lệnh con. Khi chạy API từ IDE, dùng `-SyncUserSecrets` hoặc tự cấu hình environment variables trong IDE.

Các key bắt buộc cho setup đầy đủ:

```dotenv
ConnectionStrings__PostgreSql=Host=<host>;Port=5432;Database=postgres;Username=<username>;Password="<password>";SSL Mode=Require
Frontend__Origin=http://localhost:3000
Jwt__Issuer=Portfolio.Api
Jwt__Audience=Portfolio.Frontend
Jwt__AccessTokenMinutes=10
Jwt__ClockSkewSeconds=30
Jwt__ActiveKeyId=development-local
Jwt__SigningCertificatePath=.secrets/jwt-signing-development.pfx
Jwt__SigningCertificatePassword=<generated-local-certificate-password>

RefreshToken__IdleLifetimeDays=7
RefreshToken__AbsoluteLifetimeDays=30
RefreshToken__Pepper=<generated-random-pepper>
RefreshToken__CookieName=__Host-refresh
RefreshToken__CsrfCookieName=__Host-csrf
RefreshToken__CookieSameSite=Lax

SupabaseStorage__Url=https://<project-ref>.supabase.co/
SupabaseStorage__ServiceRoleKey=<legacy-service-role-jwt>
SupabaseStorage__RequestTimeoutSeconds=30
SupabaseStorage__MaxResponseBytes=65536
SupabaseStorage__Buckets__Avatars=avatars
SupabaseStorage__Buckets__ProjectImages=project-images
SupabaseStorage__Buckets__CertificateFiles=certificate-files
SupabaseStorage__Buckets__CvFiles=cv-files
Upload__MaxFileSize=10485760
Upload__MaxHeroImageWidth=8192
Upload__MaxHeroImageHeight=8192

BootstrapAdmin__Enabled=false
BootstrapAdmin__Email=
BootstrapAdmin__Password=
```

Giá trị phải là text thuần. Không dùng Markdown URL như `[https://...](https://...)` và không viết key thành `ConnectionStrings\_\_PostgreSql`.

### JWT local

JWT trong dự án là ASP.NET Core Identity JWT, không phải Supabase Auth JWT.

- `Jwt__Issuer`: mặc định `Portfolio.Api`;
- `Jwt__Audience`: mặc định `Portfolio.Frontend`;
- `Jwt__ActiveKeyId`: `kid` của certificate đang ký;
- `Jwt__SigningCertificatePath`: RSA PFX có private key, mặc định nằm trong `.secrets/`;
- `Jwt__SigningCertificatePassword`: password ngẫu nhiên của PFX;
- `RefreshToken__Pepper`: secret HMAC độc lập, tối thiểu 32 UTF-8 bytes;
- `RefreshToken__CsrfCookieName`: cookie signed double-submit đọc được bởi frontend, bắt buộc `__Host-csrf`;
- mỗi developer phải dùng certificate và pepper local riêng;
- không dùng certificate/pepper local cho production.

Khi script tạo `.env` mới, certificate, password và pepper đều được sinh tự động. `.secrets/` đã được gitignore. Production phải dùng secret store hoặc mounted secret và quy trình key rotation trong [Authentication Guide](security/AUTHENTICATION_GUIDE.md).

## 5. User Secrets và IDE

Visual Studio/IIS Express không tự load `.env`. Lệnh onboarding khuyến nghị có `-SyncUserSecrets`:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Setup-Local.ps1 -SyncUserSecrets
```

Sau khi đồng bộ:

1. stop IIS Express/debug session;
2. restart project;
3. ASP.NET Core ở Development sẽ đọc User Secrets của `Portfolio.Api`.

Không đưa secret vào `appsettings.json` hoặc `Properties/launchSettings.json`.

Nếu member không muốn dùng User Secrets, chạy API từ cùng PowerShell process sau khi gọi script trực tiếp:

```powershell
.\scripts\Setup-Local.ps1 -ValidateOnly
dotnet run --project .\src\Portfolio.Api\Portfolio.Api.csproj
```

Không dùng `powershell.exe -File ...` cho trường hợp này vì đó là child process; environment variables sẽ mất khi child process kết thúc.

## 6. EF Core migrations

Repository pin `dotnet-ef` bằng `.config/dotnet-tools.json`. Khôi phục tool thủ công nếu cần:

```powershell
dotnet tool restore
dotnet ef --version
```

Kỳ vọng EF CLI `10.0.11`.

### Database mới

Với Supabase project mới, kiểm tra migration trước:

```powershell
.\scripts\Setup-Local.ps1 -ValidateOnly
dotnet ef migrations list --project src\Portfolio.Infrastructure --startup-project src\Portfolio.Api
```

Áp dụng migration bằng setup script:

```powershell
.\scripts\Setup-Local.ps1 -SyncUserSecrets -ApplyMigrations
```

`-ApplyMigrations` là opt-in vì migration thay đổi database dùng chung.

Migration authentication mới là `20260906132748_AddRefreshTokenSessions`. Migration này thêm `AuthVersion`/`IsDisabled` vào `AspNetUsers` và tạo `auth_sessions`, `refresh_tokens` cùng partial unique index cho một active refresh token mỗi session.

### Database đã có dữ liệu

Backup database trước. Trong Supabase SQL Editor, kiểm tra lịch sử migration:

```sql
select "MigrationId", "ProductVersion"
from public."__EFMigrationsHistory"
order by "MigrationId";
```

Nếu bảng ứng dụng đã tồn tại nhưng `__EFMigrationsHistory` không tồn tại, không chạy `database update`; cần xác định schema được tạo từ đâu.

Migration Sprint 1 yêu cầu mọi certificate đã có dữ liệu phải có `issued_date`:

```sql
select count(*) as missing_issued_dates
from public.certificates
where issued_date is null;
```

Kết quả phải bằng `0` trước khi áp dụng correction migration.

### Tạo tài khoản đăng nhập local

Setup script không trực tiếp ghi user vào database và mặc định giữ bootstrap tắt. Sau khi migrations đã được áp dụng, cấu hình một lần trong `.env` hoặc User Secrets:

```dotenv
BootstrapAdmin__Enabled=true
BootstrapAdmin__Email=<your-local-admin-email>
BootstrapAdmin__Password=
```

Đặt password thực tế bằng secret input/User Secrets; password phải dài ít nhất 12 ký tự và có chữ hoa, chữ thường, chữ số, ký tự đặc biệt. Chạy `Setup-Local.ps1 -SyncUserSecrets`, rồi khởi động API. `AdminBootstrapper` dùng ASP.NET Core Identity để tạo password hash, role `Admin` và quan hệ user-role.

Sau khi log xác nhận bootstrap thành công, đổi ngay `BootstrapAdmin__Enabled=false`, đồng bộ User Secrets và restart API. Cơ chế này idempotent nhưng không nên để bật lâu dài. Không commit email/password thật.

## 7. Chạy API

Visual Studio/IIS Express:

- HTTP: `http://localhost:49553`;
- HTTPS: `https://localhost:44313`.

CLI:

```powershell
dotnet run --project src\Portfolio.Api\Portfolio.Api.csproj
```

Launch profile CLI mặc định cung cấp:

- HTTP: `http://localhost:5038`;
- HTTPS: `https://localhost:7097`.

Swagger chỉ bật trong Development tại `http://localhost:5038/swagger`.

## 8. Health checks

Liveness, không gọi dependency:

```powershell
curl.exe -i http://localhost:5038/health
```

Readiness, kiểm tra tất cả dependency bắt buộc:

```powershell
curl.exe -i http://localhost:5038/health/ready
```

Kiểm tra riêng PostgreSQL và Supabase Storage:

```powershell
curl.exe -i http://localhost:5038/health/supabase
```

Với IIS Express:

```powershell
curl.exe -k -i https://localhost:44313/health/supabase
```

`/health/supabase` trả HTTP 200 chỉ khi PostgreSQL chấp nhận kết nối, Storage API chấp nhận server credential và cả bốn bucket cấu hình đều tồn tại. Endpoint trả 503 nếu dependency chưa cấu hình, unauthorized, timeout, provider lỗi, response không hợp lệ hoặc thiếu bucket. Response không chứa exception, URL, connection string, key, bucket contents hoặc object keys.

## 9. Tests

Unit tests:

```powershell
dotnet test tests\Portfolio.UnitTests\Portfolio.UnitTests.csproj --configuration Release
```

Integration tests yêu cầu Docker Desktop đang chạy:

```powershell
dotnet test tests\Portfolio.IntegrationTests\Portfolio.IntegrationTests.csproj --configuration Release
```

Integration suite dùng image `postgres:17-alpine`. Nếu Docker 24 báo API 1.44 quá mới, đặt biến cho process test:

```powershell
$env:DOCKER_API_VERSION = "1.43"
```

Test setup script:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/Setup-Local.Tests.ps1
```

## 10. Troubleshooting

### `Missing parameter value for 'value'`

Biến environment chưa được nạp trước khi gọi `dotnet user-secrets set`. Dùng setup script với `-SyncUserSecrets` thay vì gọi thủ công.

### `SupabaseStorage:ServiceRoleKey is required`

Chỉ Storage URL được cấu hình, nhưng server key bị thiếu. Điền legacy `service_role` key rồi chạy lại:

```powershell
.\scripts\Setup-Local.ps1 -SyncUserSecrets
```

### PostgreSQL `Network is unreachable` hoặc timeout

Direct connection của Supabase có thể yêu cầu IPv6. Chuyển sang Session pooler port `5432` và dùng chính xác username do Connect dialog cung cấp.

### `/health/supabase` trả Storage `unhealthy`

Kiểm tra:

1. `SupabaseStorage__Url` là URL HTTPS thuần và có đúng project ref;
2. legacy `service_role` key còn hiệu lực;
3. bốn bucket tồn tại với tên chính xác;
4. máy có thể gọi HTTPS tới Supabase;
5. restart IIS Express sau khi cập nhật User Secrets.

### `dotnet ef` không tồn tại

Chạy tại repository root:

```powershell
dotnet tool restore
```

## 11. Quy tắc bảo mật

- Không commit `.env` hoặc `appsettings.Local.json`.
- Không paste database password, JWT signing key hoặc service key vào issue, PR, chat hoặc log.
- Không chạy `dotnet user-secrets list` khi share màn hình.
- Không dùng Storage service credential trong frontend.
- Rotate ngay credential nếu bị lộ.
- Production secrets phải nằm trong secret store/dashboard của nền tảng deploy, không dùng file `.env` được đóng gói vào image.
