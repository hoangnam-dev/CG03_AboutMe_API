using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Portfolio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "certificates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    issued_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expiration_date = table.Column<DateOnly>(type: "date", nullable: true),
                    credential_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    credential_url = table.Column<string>(type: "text", nullable: true),
                    file_url = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificates", x => x.id);
                    table.CheckConstraint("ck_certificates_dates", "expiration_date IS NULL OR issued_date IS NULL OR expiration_date >= issued_date");
                    table.CheckConstraint("ck_certificates_display_order", "display_order >= 0");
                });

            migrationBuilder.CreateTable(
                name: "contact_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    sender_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    sender_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "new"),
                    is_spam = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ip_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_messages", x => x.id);
                    table.CheckConstraint("ck_contact_messages_status", "status IN ('new', 'read', 'archived')");
                });

            migrationBuilder.CreateTable(
                name: "profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    hero_image_url = table.Column<string>(type: "text", nullable: true),
                    available_for_work = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    internal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "personal"),
                    disclosure_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "full"),
                    thumbnail_url = table.Column<string>(type: "text", nullable: true),
                    repository_url = table.Column<string>(type: "text", nullable: true),
                    demo_url = table.Column<string>(type: "text", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_featured = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.id);
                    table.CheckConstraint("ck_projects_dates", "end_date IS NULL OR start_date IS NULL OR end_date >= start_date");
                    table.CheckConstraint("ck_projects_disclosure_level", "disclosure_level IN ('full', 'limited')");
                    table.CheckConstraint("ck_projects_display_order", "display_order >= 0");
                    table.CheckConstraint("ck_projects_kind", "kind IN ('personal', 'professional')");
                });

            migrationBuilder.CreateTable(
                name: "resume_version_counters",
                columns: table => new
                {
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    version_year = table.Column<short>(type: "smallint", nullable: false),
                    last_sequence = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resume_version_counters", x => new { x.language_code, x.version_year });
                    table.CheckConstraint("ck_resume_version_counters_locale", "language_code IN ('en', 'vi')");
                    table.CheckConstraint("ck_resume_version_counters_sequence", "last_sequence > 0");
                    table.CheckConstraint("ck_resume_version_counters_year", "version_year BETWEEN 2000 AND 9999");
                });

            migrationBuilder.CreateTable(
                name: "site_settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "skill_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_categories", x => x.id);
                    table.CheckConstraint("ck_skill_categories_display_order", "display_order >= 0");
                });

            migrationBuilder.CreateTable(
                name: "work_experiences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    employment_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    company_url = table.Column<string>(type: "text", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_experiences", x => x.id);
                    table.CheckConstraint("ck_work_experiences_dates", "end_date IS NULL OR end_date >= start_date");
                    table.CheckConstraint("ck_work_experiences_display_order", "display_order >= 0");
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "certificate_translations",
                columns: table => new
                {
                    certificate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificate_translations", x => new { x.certificate_id, x.locale_code });
                    table.CheckConstraint("ck_certificate_translations_locale", "locale_code IN ('en', 'vi')");
                    table.ForeignKey(
                        name: "FK_certificate_translations_certificates_certificate_id",
                        column: x => x.certificate_id,
                        principalTable: "certificates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "abouts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    years_of_experience = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false, defaultValue: 0m),
                    project_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    technology_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    show_years_of_experience = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    show_project_count = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    show_technology_count = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    show_contact_section = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_abouts", x => x.id);
                    table.CheckConstraint("ck_abouts_project_count", "project_count >= 0");
                    table.CheckConstraint("ck_abouts_technology_count", "technology_count >= 0");
                    table.CheckConstraint("ck_abouts_years_of_experience", "years_of_experience >= 0");
                    table.ForeignKey(
                        name: "FK_abouts_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profile_translations",
                columns: table => new
                {
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    short_bio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    availability = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_translations", x => new { x.profile_id, x.locale_code });
                    table.CheckConstraint("ck_profile_translations_locale", "locale_code IN ('en', 'vi')");
                    table.ForeignKey(
                        name: "FK_profile_translations_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "social_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    url = table.Column<string>(type: "text", nullable: false),
                    icon_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_social_links", x => x.id);
                    table.CheckConstraint("ck_social_links_display_order", "display_order >= 0");
                    table.ForeignKey(
                        name: "FK_social_links_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_highlights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_highlights", x => x.id);
                    table.CheckConstraint("ck_project_highlights_display_order", "display_order >= 0");
                    table.CheckConstraint("ck_project_highlights_locale", "locale_code IN ('en', 'vi')");
                    table.ForeignKey(
                        name: "FK_project_highlights_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    image_url = table.Column<string>(type: "text", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_images", x => x.id);
                    table.CheckConstraint("ck_project_images_display_order", "display_order >= 0");
                    table.ForeignKey(
                        name: "FK_project_images_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_translations",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    short_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    client_context = table.Column<string>(type: "text", nullable: true),
                    role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    problem = table.Column<string>(type: "text", nullable: true),
                    solution = table.Column<string>(type: "text", nullable: true),
                    result = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_translations", x => new { x.project_id, x.locale_code });
                    table.CheckConstraint("ck_project_translations_locale", "locale_code IN ('en', 'vi')");
                    table.ForeignKey(
                        name: "FK_project_translations_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resumes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    file_url = table.Column<string>(type: "text", nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    version_year = table.Column<short>(type: "smallint", nullable: false),
                    version_sequence = table.Column<int>(type: "integer", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resumes", x => x.id);
                    table.UniqueConstraint("ak_resumes_language_year_sequence", x => new { x.language_code, x.version_year, x.version_sequence });
                    table.CheckConstraint("ck_resumes_display_order", "display_order >= 0");
                    table.CheckConstraint("ck_resumes_file_size", "file_size > 0");
                    table.CheckConstraint("ck_resumes_locale", "language_code IN ('en', 'vi')");
                    table.CheckConstraint("ck_resumes_sequence", "version_sequence > 0");
                    table.CheckConstraint("ck_resumes_year", "version_year BETWEEN 2000 AND 9999");
                    table.ForeignKey(
                        name: "FK_resumes_resume_version_counters_language_code_version_year",
                        columns: x => new { x.language_code, x.version_year },
                        principalTable: "resume_version_counters",
                        principalColumns: new[] { "language_code", "version_year" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "skill_category_translations",
                columns: table => new
                {
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_category_translations", x => new { x.category_id, x.locale_code });
                    table.CheckConstraint("ck_skill_category_translations_locale", "locale_code IN ('en', 'vi')");
                    table.ForeignKey(
                        name: "FK_skill_category_translations_skill_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "skill_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "technologies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    years_of_experience = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    icon_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "text"),
                    icon_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_technologies", x => x.id);
                    table.CheckConstraint("ck_technologies_display_order", "display_order >= 0");
                    table.CheckConstraint("ck_technologies_icon_type", "icon_type IN ('lucide', 'image', 'text')");
                    table.CheckConstraint("ck_technologies_icon_value", "(icon_type = 'text' AND char_length(btrim(icon_value)) BETWEEN 1 AND 6) OR (icon_type = 'lucide' AND char_length(btrim(icon_value)) BETWEEN 1 AND 100 AND icon_value ~ '^[a-z0-9]+(-[a-z0-9]+)*$') OR (icon_type = 'image' AND char_length(btrim(icon_value)) BETWEEN 1 AND 500)");
                    table.CheckConstraint("ck_technologies_skill_level", "skill_level IN ('Primary', 'Experienced', 'Familiar', 'Learning')");
                    table.CheckConstraint("ck_technologies_years_of_experience", "years_of_experience IS NULL OR years_of_experience >= 0");
                    table.ForeignKey(
                        name: "FK_technologies_skill_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "skill_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "experience_highlights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    experience_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    highlight_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experience_highlights", x => x.id);
                    table.CheckConstraint("ck_experience_highlights_display_order", "display_order >= 0");
                    table.CheckConstraint("ck_experience_highlights_locale", "locale_code IN ('en', 'vi')");
                    table.CheckConstraint("ck_experience_highlights_type", "highlight_type IN ('responsibility', 'achievement')");
                    table.ForeignKey(
                        name: "FK_experience_highlights_work_experiences_experience_id",
                        column: x => x.experience_id,
                        principalTable: "work_experiences",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "experience_translations",
                columns: table => new
                {
                    experience_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    position = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experience_translations", x => new { x.experience_id, x.locale_code });
                    table.CheckConstraint("ck_experience_translations_locale", "locale_code IN ('en', 'vi')");
                    table.ForeignKey(
                        name: "FK_experience_translations_work_experiences_experience_id",
                        column: x => x.experience_id,
                        principalTable: "work_experiences",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "about_translations",
                columns: table => new
                {
                    about_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    content = table.Column<string>(type: "text", nullable: true),
                    career_goal = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_about_translations", x => new { x.about_id, x.locale_code });
                    table.CheckConstraint("ck_about_translations_locale", "locale_code IN ('en', 'vi')");
                    table.ForeignKey(
                        name: "FK_about_translations_abouts_about_id",
                        column: x => x.about_id,
                        principalTable: "abouts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_image_translations",
                columns: table => new
                {
                    project_image_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    alt_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_image_translations", x => new { x.project_image_id, x.locale_code });
                    table.CheckConstraint("ck_project_image_translations_locale", "locale_code IN ('en', 'vi')");
                    table.ForeignKey(
                        name: "FK_project_image_translations_project_images_project_image_id",
                        column: x => x.project_image_id,
                        principalTable: "project_images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resume_translations",
                columns: table => new
                {
                    resume_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resume_translations", x => new { x.resume_id, x.locale_code });
                    table.CheckConstraint("ck_resume_translations_locale", "locale_code IN ('en', 'vi')");
                    table.ForeignKey(
                        name: "FK_resume_translations_resumes_resume_id",
                        column: x => x.resume_id,
                        principalTable: "resumes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "certificate_technologies",
                columns: table => new
                {
                    certificate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    technology_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificate_technologies", x => new { x.certificate_id, x.technology_id });
                    table.ForeignKey(
                        name: "FK_certificate_technologies_certificates_certificate_id",
                        column: x => x.certificate_id,
                        principalTable: "certificates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_certificate_technologies_technologies_technology_id",
                        column: x => x.technology_id,
                        principalTable: "technologies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "experience_technologies",
                columns: table => new
                {
                    experience_id = table.Column<Guid>(type: "uuid", nullable: false),
                    technology_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experience_technologies", x => new { x.experience_id, x.technology_id });
                    table.CheckConstraint("ck_experience_technologies_display_order", "display_order >= 0");
                    table.ForeignKey(
                        name: "FK_experience_technologies_technologies_technology_id",
                        column: x => x.technology_id,
                        principalTable: "technologies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_experience_technologies_work_experiences_experience_id",
                        column: x => x.experience_id,
                        principalTable: "work_experiences",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_technologies",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    technology_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_technologies", x => new { x.project_id, x.technology_id });
                    table.CheckConstraint("ck_project_technologies_display_order", "display_order >= 0");
                    table.ForeignKey(
                        name: "FK_project_technologies_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_technologies_technologies_technology_id",
                        column: x => x.technology_id,
                        principalTable: "technologies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "technology_translations",
                columns: table => new
                {
                    technology_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_technology_translations", x => new { x.technology_id, x.locale_code });
                    table.CheckConstraint("ck_technology_translations_locale", "locale_code IN ('en', 'vi')");
                    table.ForeignKey(
                        name: "FK_technology_translations_technologies_technology_id",
                        column: x => x.technology_id,
                        principalTable: "technologies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_abouts_profile_id",
                table: "abouts",
                column: "profile_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_certificate_technologies_technology_id",
                table: "certificate_technologies",
                column: "technology_id");

            migrationBuilder.CreateIndex(
                name: "idx_certificates_public_order",
                table: "certificates",
                columns: new[] { "display_order", "issued_date" },
                descending: new[] { false, true },
                filter: "is_published");

            migrationBuilder.CreateIndex(
                name: "idx_contact_messages_admin_filter",
                table: "contact_messages",
                columns: new[] { "status", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_experience_highlights_order",
                table: "experience_highlights",
                columns: new[] { "experience_id", "locale_code", "highlight_type", "display_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_experience_technologies_technology_id",
                table: "experience_technologies",
                column: "technology_id");

            migrationBuilder.CreateIndex(
                name: "IX_profiles_slug",
                table: "profiles",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_project_highlights_order",
                table: "project_highlights",
                columns: new[] { "project_id", "locale_code", "display_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_project_images_order",
                table: "project_images",
                columns: new[] { "project_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "IX_project_technologies_technology_id",
                table: "project_technologies",
                column: "technology_id");

            migrationBuilder.CreateIndex(
                name: "idx_projects_public_filter_order",
                table: "projects",
                columns: new[] { "kind", "disclosure_level", "is_featured", "display_order" },
                descending: new[] { false, false, true, false },
                filter: "is_published");

            migrationBuilder.CreateIndex(
                name: "IX_projects_slug",
                table: "projects",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_resumes_public_language_version",
                table: "resumes",
                columns: new[] { "language_code", "version_year", "version_sequence" },
                descending: new[] { false, true, true },
                filter: "is_published");

            migrationBuilder.CreateIndex(
                name: "ux_resumes_one_active_per_language",
                table: "resumes",
                column: "language_code",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_skill_categories_public_order",
                table: "skill_categories",
                column: "display_order",
                filter: "is_published");

            migrationBuilder.CreateIndex(
                name: "idx_social_links_public_order",
                table: "social_links",
                column: "display_order",
                filter: "is_published");

            migrationBuilder.CreateIndex(
                name: "IX_social_links_profile_id_platform",
                table: "social_links",
                columns: new[] { "profile_id", "platform" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_technologies_public_category_order",
                table: "technologies",
                columns: new[] { "category_id", "display_order" },
                filter: "is_published");

            migrationBuilder.CreateIndex(
                name: "IX_technology_translations_locale_code_name",
                table: "technology_translations",
                columns: new[] { "locale_code", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_work_experiences_public_order",
                table: "work_experiences",
                columns: new[] { "display_order", "start_date" },
                descending: new[] { false, true },
                filter: "is_published");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "about_translations");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "certificate_technologies");

            migrationBuilder.DropTable(
                name: "certificate_translations");

            migrationBuilder.DropTable(
                name: "contact_messages");

            migrationBuilder.DropTable(
                name: "experience_highlights");

            migrationBuilder.DropTable(
                name: "experience_technologies");

            migrationBuilder.DropTable(
                name: "experience_translations");

            migrationBuilder.DropTable(
                name: "profile_translations");

            migrationBuilder.DropTable(
                name: "project_highlights");

            migrationBuilder.DropTable(
                name: "project_image_translations");

            migrationBuilder.DropTable(
                name: "project_technologies");

            migrationBuilder.DropTable(
                name: "project_translations");

            migrationBuilder.DropTable(
                name: "resume_translations");

            migrationBuilder.DropTable(
                name: "site_settings");

            migrationBuilder.DropTable(
                name: "skill_category_translations");

            migrationBuilder.DropTable(
                name: "social_links");

            migrationBuilder.DropTable(
                name: "technology_translations");

            migrationBuilder.DropTable(
                name: "abouts");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "certificates");

            migrationBuilder.DropTable(
                name: "work_experiences");

            migrationBuilder.DropTable(
                name: "project_images");

            migrationBuilder.DropTable(
                name: "resumes");

            migrationBuilder.DropTable(
                name: "technologies");

            migrationBuilder.DropTable(
                name: "profiles");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "resume_version_counters");

            migrationBuilder.DropTable(
                name: "skill_categories");
        }
    }
}
