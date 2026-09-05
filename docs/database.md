# Portfolio Database Design — PostgreSQL

## 1. Mục tiêu và quy ước

Database phục vụ portfolio song ngữ Anh (`en`)/Việt (`vi`), Admin CRUD, project có kiểm soát mức công khai, CV có version tự động và Contact inbox.

- PostgreSQL; khóa chính nghiệp vụ dùng `uuid`; thời gian dùng `timestamptz`.
- Nội dung dịch nằm trong bảng `*_translations`, khóa chính `(entity_id, locale_code)`.
- File nằm trên object storage; database chỉ lưu path/URL và metadata.
- `created_at` của Resume là thời điểm upload bất biến. Timezone cấp version là `Asia/Ho_Chi_Minh`.
- ASP.NET Core Identity quản lý tài khoản/role Admin và không được định nghĩa lại tại đây.
- MVP chỉ có một Portfolio; content không có owner FK. Mở rộng multi-Portfolio cần migration/ADR riêng.
- Public API chỉ trả nội dung đã publish và phải loại dữ liệu nhạy cảm của project `limited`.
- Các hiệu chỉnh sau initial migration được quyết định tại `docs/adr/0001-mvp-contract-decisions.md`; không sửa migration đã tạo.

## 2. Danh sách bảng và ánh xạ FE

| Model FE | Bảng |
| --- | --- |
| `Profile` | `profiles`, `profile_translations`, `social_links` |
| `About` | `abouts`, `about_translations` |
| `SkillCategory`, `Skill` | `skill_categories`, `skill_category_translations`, `technologies`, `technology_translations` |
| `Experience` | `work_experiences`, `experience_translations`, `experience_highlights`, `experience_technologies` |
| `AdminProject`, `ProjectImage` | `projects`, `project_translations`, `project_technologies`, `project_highlights`, `project_images`, `project_image_translations` |
| `Certificate` | `certificates`, `certificate_translations`, `certificate_technologies` |
| `CvFile` | `resumes`, `resume_translations`, `resume_version_counters` |
| `ContactMessage` | `contact_messages` |

`DashboardStats`, pagination, API wrapper, signed `downloadUrl`, `publishConfirmed` và multipart `fileIndex` là dữ liệu tính toán/tạm thời, không có column riêng. `Experience.isCurrent` được suy ra từ `end_date IS NULL`.

## 3. PostgreSQL DDL

```sql
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE profiles (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    slug varchar(220) NOT NULL UNIQUE,
    full_name varchar(150) NOT NULL,
    email varchar(320), phone varchar(30),
    show_email boolean NOT NULL DEFAULT false,
    show_phone boolean NOT NULL DEFAULT false,
    avatar_url text, hero_image_url text,
    available_for_work boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE profile_translations (
    profile_id uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    locale_code varchar(10) NOT NULL CHECK (locale_code IN ('en', 'vi')),
    title varchar(200) NOT NULL,
    short_bio varchar(500), location varchar(200), availability varchar(250),
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (profile_id, locale_code)
);

CREATE TABLE social_links (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    profile_id uuid NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    platform varchar(50) NOT NULL, label varchar(100), url text NOT NULL,
    icon_name varchar(100),
    display_order integer NOT NULL DEFAULT 0 CHECK (display_order >= 0),
    is_published boolean NOT NULL DEFAULT false,
    UNIQUE (profile_id, platform)
);

CREATE TABLE abouts (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    profile_id uuid NOT NULL UNIQUE REFERENCES profiles(id) ON DELETE CASCADE,
    years_of_experience numeric(4,1) NOT NULL DEFAULT 0 CHECK (years_of_experience >= 0),
    project_count integer NOT NULL DEFAULT 0 CHECK (project_count >= 0),
    technology_count integer NOT NULL DEFAULT 0 CHECK (technology_count >= 0),
    show_years_of_experience boolean NOT NULL DEFAULT true,
    show_project_count boolean NOT NULL DEFAULT true,
    show_technology_count boolean NOT NULL DEFAULT true,
    show_contact_section boolean NOT NULL DEFAULT true,
    is_published boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE about_translations (
    about_id uuid NOT NULL REFERENCES abouts(id) ON DELETE CASCADE,
    locale_code varchar(10) NOT NULL CHECK (locale_code IN ('en', 'vi')),
    content text, career_goal text,
    PRIMARY KEY (about_id, locale_code)
);

CREATE TABLE skill_categories (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    display_order integer NOT NULL DEFAULT 0 CHECK (display_order >= 0),
    is_published boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE skill_category_translations (
    category_id uuid NOT NULL REFERENCES skill_categories(id) ON DELETE CASCADE,
    locale_code varchar(10) NOT NULL CHECK (locale_code IN ('en', 'vi')),
    name varchar(100) NOT NULL,
    PRIMARY KEY (category_id, locale_code)
);

CREATE TABLE technologies (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    category_id uuid NOT NULL REFERENCES skill_categories(id) ON DELETE RESTRICT,
    skill_level varchar(20) NOT NULL CHECK (skill_level IN ('Primary', 'Experienced', 'Familiar', 'Learning')),
    years_of_experience numeric(4,1) CHECK (years_of_experience >= 0),
    icon_type varchar(20) NOT NULL DEFAULT 'text'
        CHECK (icon_type IN ('lucide', 'image', 'text')),
    icon_value varchar(500) NOT NULL,
    display_order integer NOT NULL DEFAULT 0 CHECK (display_order >= 0),
    is_published boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_technologies_icon_value CHECK (
        (icon_type = 'text' AND char_length(btrim(icon_value)) BETWEEN 1 AND 6)
        OR
        (icon_type = 'lucide'
            AND char_length(btrim(icon_value)) BETWEEN 1 AND 100
            AND icon_value ~ '^[a-z0-9]+(-[a-z0-9]+)*$')
        OR
        (icon_type = 'image'
            AND char_length(btrim(icon_value)) BETWEEN 1 AND 500)
    )
);

CREATE TABLE technology_translations (
    technology_id uuid NOT NULL REFERENCES technologies(id) ON DELETE CASCADE,
    locale_code varchar(10) NOT NULL CHECK (locale_code IN ('en', 'vi')),
    name varchar(100) NOT NULL,
    PRIMARY KEY (technology_id, locale_code),
    UNIQUE (locale_code, name)
);

CREATE TABLE work_experiences (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_name varchar(200) NOT NULL,
    employment_type varchar(30), company_url text,
    start_date date NOT NULL, end_date date,
    display_order integer NOT NULL DEFAULT 0 CHECK (display_order >= 0),
    is_published boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CHECK (end_date IS NULL OR end_date >= start_date)
);

CREATE TABLE experience_translations (
    experience_id uuid NOT NULL REFERENCES work_experiences(id) ON DELETE CASCADE,
    locale_code varchar(10) NOT NULL CHECK (locale_code IN ('en', 'vi')),
    position varchar(150) NOT NULL, location varchar(200), description text,
    PRIMARY KEY (experience_id, locale_code)
);

CREATE TABLE experience_highlights (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    experience_id uuid NOT NULL REFERENCES work_experiences(id) ON DELETE CASCADE,
    locale_code varchar(10) NOT NULL CHECK (locale_code IN ('en', 'vi')),
    highlight_type varchar(20) NOT NULL CHECK (highlight_type IN ('responsibility', 'achievement')),
    content text NOT NULL,
    display_order integer NOT NULL DEFAULT 0 CHECK (display_order >= 0),
    UNIQUE (experience_id, locale_code, highlight_type, display_order)
);

CREATE TABLE experience_technologies (
    experience_id uuid NOT NULL REFERENCES work_experiences(id) ON DELETE CASCADE,
    technology_id uuid NOT NULL REFERENCES technologies(id) ON DELETE RESTRICT,
    display_order integer NOT NULL DEFAULT 0 CHECK (display_order >= 0),
    PRIMARY KEY (experience_id, technology_id)
);

CREATE TABLE projects (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    slug varchar(220) NOT NULL UNIQUE,
    internal_name varchar(200) NOT NULL,
    kind varchar(20) NOT NULL DEFAULT 'personal' CHECK (kind IN ('personal', 'professional')),
    disclosure_level varchar(20) NOT NULL DEFAULT 'full' CHECK (disclosure_level IN ('full', 'limited')),
    thumbnail_url text, repository_url text, demo_url text,
    start_date date, end_date date,
    is_featured boolean NOT NULL DEFAULT false,
    display_order integer NOT NULL DEFAULT 0 CHECK (display_order >= 0),
    is_published boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CHECK (end_date IS NULL OR start_date IS NULL OR end_date >= start_date)
);

CREATE TABLE project_translations (
    project_id uuid NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    locale_code varchar(10) NOT NULL CHECK (locale_code IN ('en', 'vi')),
    name varchar(200) NOT NULL,
    short_description varchar(500), client_context text, role varchar(200),
    problem text, solution text, result text,
    PRIMARY KEY (project_id, locale_code)
);

CREATE TABLE project_technologies (
    project_id uuid NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    technology_id uuid NOT NULL REFERENCES technologies(id) ON DELETE RESTRICT,
    display_order integer NOT NULL DEFAULT 0 CHECK (display_order >= 0),
    PRIMARY KEY (project_id, technology_id)
);

CREATE TABLE project_highlights (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id uuid NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    locale_code varchar(10) NOT NULL CHECK (locale_code IN ('en', 'vi')),
    content text NOT NULL,
    display_order integer NOT NULL DEFAULT 0 CHECK (display_order >= 0),
    UNIQUE (project_id, locale_code, display_order)
);

CREATE TABLE project_images (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id uuid NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    image_url text NOT NULL,
    display_order integer NOT NULL DEFAULT 0 CHECK (display_order >= 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE project_image_translations (
    project_image_id uuid NOT NULL REFERENCES project_images(id) ON DELETE CASCADE,
    locale_code varchar(10) NOT NULL CHECK (locale_code IN ('en', 'vi')),
    alt_text varchar(500) NOT NULL,
    PRIMARY KEY (project_image_id, locale_code)
);

CREATE TABLE certificates (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    issuer varchar(200) NOT NULL,
    issued_date date NOT NULL, expiration_date date,
    credential_id varchar(200), credential_url text,
    show_credential_id boolean NOT NULL DEFAULT false,
    file_url text, image_url text,
    display_order integer NOT NULL DEFAULT 0 CHECK (display_order >= 0),
    is_published boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CHECK (expiration_date IS NULL OR expiration_date >= issued_date)
);

CREATE TABLE certificate_translations (
    certificate_id uuid NOT NULL REFERENCES certificates(id) ON DELETE CASCADE,
    locale_code varchar(10) NOT NULL CHECK (locale_code IN ('en', 'vi')),
    name varchar(200) NOT NULL,
    PRIMARY KEY (certificate_id, locale_code)
);

CREATE TABLE certificate_technologies (
    certificate_id uuid NOT NULL REFERENCES certificates(id) ON DELETE CASCADE,
    technology_id uuid NOT NULL REFERENCES technologies(id) ON DELETE RESTRICT,
    PRIMARY KEY (certificate_id, technology_id)
);

CREATE TABLE resume_version_counters (
    language_code varchar(10) NOT NULL CHECK (language_code IN ('en', 'vi')),
    version_year smallint NOT NULL CHECK (version_year BETWEEN 2000 AND 9999),
    last_sequence integer NOT NULL CHECK (last_sequence > 0),
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (language_code, version_year)
);

CREATE TABLE resumes (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    language_code varchar(10) NOT NULL CHECK (language_code IN ('en', 'vi')),
    file_url text NOT NULL,
    original_file_name varchar(255) NOT NULL,
    content_type varchar(100) NOT NULL,
    file_size bigint NOT NULL CHECK (file_size > 0),
    version_year smallint NOT NULL CHECK (version_year BETWEEN 2000 AND 9999),
    version_sequence integer NOT NULL CHECK (version_sequence > 0),
    display_order integer NOT NULL DEFAULT 0 CHECK (display_order >= 0),
    is_published boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (language_code, version_year, version_sequence),
    FOREIGN KEY (language_code, version_year)
        REFERENCES resume_version_counters(language_code, version_year)
);

CREATE TABLE resume_translations (
    resume_id uuid NOT NULL REFERENCES resumes(id) ON DELETE CASCADE,
    locale_code varchar(10) NOT NULL CHECK (locale_code IN ('en', 'vi')),
    description varchar(500),
    PRIMARY KEY (resume_id, locale_code)
);

CREATE TABLE contact_messages (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    sender_name varchar(150) NOT NULL,
    sender_email varchar(320) NOT NULL,
    subject varchar(200) NOT NULL,
    message text NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'new' CHECK (status IN ('new', 'read', 'archived')),
    is_spam boolean NOT NULL DEFAULT false,
    ip_hash varchar(128), user_agent varchar(500),
    created_at timestamptz NOT NULL DEFAULT now(), read_at timestamptz
);

CREATE TABLE site_settings (
    key varchar(100) PRIMARY KEY,
    value jsonb NOT NULL,
    description varchar(500),
    updated_at timestamptz NOT NULL DEFAULT now()
);
```

## 4. Index

```sql
CREATE INDEX idx_social_links_public_order ON social_links (display_order) WHERE is_published;
CREATE INDEX idx_skill_categories_public_order ON skill_categories (display_order) WHERE is_published;
CREATE INDEX idx_technologies_public_category_order ON technologies (category_id, display_order) WHERE is_published;
CREATE INDEX idx_work_experiences_public_order ON work_experiences (display_order, start_date DESC) WHERE is_published;
CREATE INDEX idx_experience_highlights_order ON experience_highlights (experience_id, locale_code, highlight_type, display_order);
CREATE INDEX idx_projects_public_filter_order ON projects (kind, disclosure_level, is_featured DESC, display_order) WHERE is_published;
CREATE INDEX idx_project_highlights_order ON project_highlights (project_id, locale_code, display_order);
CREATE INDEX idx_project_images_order ON project_images (project_id, display_order);
CREATE INDEX idx_certificates_public_order ON certificates (display_order, issued_date DESC) WHERE is_published;
CREATE UNIQUE INDEX ux_resumes_one_active_per_language ON resumes (language_code) WHERE is_active;
CREATE INDEX idx_resumes_public_language_version ON resumes (language_code, version_year DESC, version_sequence DESC) WHERE is_published;
CREATE INDEX idx_contact_messages_admin_filter ON contact_messages (status, created_at DESC);
```

### 4.1 Chuyển đổi icon kỹ năng cũ

Nếu database đã có cột `technologies.icon_url`, không đổi tên trực tiếp vì dữ liệu cũ có thể đang trộn URL, tên Lucide và text viết tắt. Migration cần thêm hai cột nullable, backfill theo mapping đã được duyệt, kiểm tra dữ liệu, rồi mới bật `NOT NULL`, constraint và xóa cột cũ.

```sql
ALTER TABLE technologies
    ADD COLUMN icon_type varchar(20),
    ADD COLUMN icon_value varchar(500);

-- Backfill minh họa; bổ sung đầy đủ mapping theo dữ liệu production.
UPDATE technologies
SET
    icon_type = CASE
        WHEN icon_url LIKE '/%' OR icon_url LIKE 'https://%' THEN 'image'
        WHEN icon_url IN ('atom', 'blocks', 'boxes', 'code-xml', 'database',
                          'database-zap', 'git-branch', 'layers', 'panel-top',
                          'triangle', 'wind') THEN 'lucide'
        ELSE 'text'
    END,
    icon_value = btrim(icon_url)
WHERE icon_url IS NOT NULL;

-- Dừng migration nếu còn NULL, text dài hơn 6 ký tự hoặc Lucide ngoài allowlist.
-- Sau khi dữ liệu hợp lệ: SET NOT NULL, thêm các CHECK như DDL mục 3, rồi DROP icon_url.
```

Validation URL/host/MIME của `image` thuộc Application layer; database chỉ giữ giới hạn độ dài và tính nhất quán giữa `icon_type` với `icon_value`.

## 5. Cấp Resume version

Client không gửi version. Mỗi file mới tạo một row Resume mới. Backend lấy năm theo timezone nghiệp vụ và cấp sequence trong cùng transaction với việc lưu metadata:

```sql
-- @year = EXTRACT(YEAR FROM CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Ho_Chi_Minh')
INSERT INTO resume_version_counters (language_code, version_year, last_sequence)
VALUES (@language, @year, 1)
ON CONFLICT (language_code, version_year)
DO UPDATE SET
    last_sequence = resume_version_counters.last_sequence + 1,
    updated_at = now()
RETURNING last_sequence;
```

Backend insert Resume bằng `version_year` và sequence vừa nhận. Nếu bản mới là current, backend tắt bản current cũ cùng ngôn ngữ trong transaction đó. API tạo field chỉ đọc:

```text
version = "v" + version_year + "_" + version_sequence.padStart(2, "0")
```

Ví dụ `v2026_07`, `v2026_08`; lần upload đầu tiên năm 2027 tự thành `v2027_01`.

Khi migrate dữ liệu Resume có sẵn, khởi tạo counter từ sequence lớn nhất để lần upload tiếp theo không trùng version:

```sql
INSERT INTO resume_version_counters (language_code, version_year, last_sequence)
SELECT language_code, version_year, MAX(version_sequence)
FROM resumes
GROUP BY language_code, version_year
ON CONFLICT (language_code, version_year)
DO UPDATE SET last_sequence = GREATEST(
    resume_version_counters.last_sequence,
    EXCLUDED.last_sequence
);
```

- EN và VI có bộ đếm độc lập.
- Không dùng `updated_at` để tính version.
- Xóa Resume không tái sử dụng version cũ.
- Upload lỗi rollback cả Resume và lần tăng counter.
- Sửa metadata, publish hoặc current không tăng version.
- Thay file là tạo Resume version mới, không ghi đè file cũ.

## 6. Quy tắc Backend

1. Chỉ Admin được create/update/delete/publish.
2. Publish yêu cầu đủ translation EN/VI theo validation của resource.
3. Public API chỉ trả dữ liệu `is_published = true`.
4. Project `limited` phải được sanitize ở server, không chỉ ẩn bằng CSS.
5. Backend kiểm tra MIME/dung lượng và tự tạo storage path; không nhận URL tùy ý.
6. Slug được chuẩn hóa chữ thường và kiểm tra unique.
7. Contact API có rate limit, honeypot, email validation và giới hạn độ dài.
8. MVP lưu/trả plain text. Nếu bổ sung Markdown/rich text sau này, phải chốt field, cú pháp và sanitizer trước khi triển khai.
9. Cấp Resume version và đổi Resume current phải chạy trong transaction.
10. Email/phone Profile và credential ID Certificate chỉ public khi visibility flag tương ứng là `true`.

## 7. Ngoài phạm vi MVP

Blog, tags, comments, testimonials, analytics chi tiết, audit log đầy đủ, soft delete và locale ngoài `en`/`vi` cần migration riêng khi được triển khai.
