\set ON_ERROR_STOP on

-- Run once as the database owner/migration identity after taking the ACL snapshot
-- documented in SPRINT_9_SECURITY_CHECKLIST.md. Passwords and login identities are
-- provisioned in the platform secret store and are intentionally absent here.

BEGIN;

DO $roles$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'portfolio_api') THEN
        CREATE ROLE portfolio_api NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'portfolio_migrator') THEN
        CREATE ROLE portfolio_migrator NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
    END IF;
END
$roles$;

-- GATE-13: Supabase Data API roles are not an application access path.
-- Revoke PUBLIC as well because direct revokes do not remove privileges inherited
-- through the implicit PUBLIC role.
REVOKE USAGE ON SCHEMA public FROM PUBLIC, anon, authenticated;
REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA public FROM PUBLIC, anon, authenticated;
REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public FROM PUBLIC, anon, authenticated;
REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA public FROM PUBLIC, anon, authenticated;

GRANT USAGE ON SCHEMA public TO portfolio_api;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
    "AspNetRoleClaims",
    "AspNetRoles",
    "AspNetUserClaims",
    "AspNetUserLogins",
    "AspNetUserRoles",
    "AspNetUsers",
    "AspNetUserTokens",
    auth_sessions,
    refresh_tokens,
    profiles,
    profile_translations,
    social_links,
    abouts,
    about_translations,
    skill_categories,
    skill_category_translations,
    technologies,
    technology_translations,
    work_experiences,
    experience_translations,
    experience_highlights,
    experience_technologies,
    projects,
    project_translations,
    project_technologies,
    project_highlights,
    project_images,
    project_image_translations,
    certificates,
    certificate_translations,
    certificate_technologies,
    resume_version_counters,
    resumes,
    resume_translations,
    contact_messages,
    site_settings
TO portfolio_api;

-- EF migrations run under the separately held migration identity. The executing
-- owner retains ownership; this role receives the privileges needed by the
-- controlled migration runner without granting them to the runtime role.
GRANT USAGE, CREATE ON SCHEMA public TO portfolio_migrator;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO portfolio_migrator;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO portfolio_migrator;
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO portfolio_migrator;

-- PostgreSQL permits changing another role's default privileges only when the
-- executing identity is a member of that role. ADMIN OPTION is retained by the
-- database owner so it can provision the separately held migration login.
GRANT portfolio_migrator TO CURRENT_USER WITH ADMIN OPTION;

-- These defaults apply to objects subsequently created by portfolio_migrator.
ALTER DEFAULT PRIVILEGES FOR ROLE portfolio_migrator IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO portfolio_api;
ALTER DEFAULT PRIVILEGES FOR ROLE portfolio_migrator IN SCHEMA public
    REVOKE ALL ON TABLES FROM PUBLIC, anon, authenticated;
ALTER DEFAULT PRIVILEGES FOR ROLE portfolio_migrator IN SCHEMA public
    REVOKE ALL ON SEQUENCES FROM PUBLIC, anon, authenticated;
ALTER DEFAULT PRIVILEGES FOR ROLE portfolio_migrator IN SCHEMA public
    REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC, anon, authenticated;

COMMIT;

-- Verification (must return false/false for Data API roles and true for runtime):
SELECT
    has_schema_privilege('anon', 'public', 'USAGE') AS anon_schema_usage,
    has_schema_privilege('authenticated', 'public', 'USAGE') AS authenticated_schema_usage,
    has_table_privilege('portfolio_api', 'public.projects', 'SELECT') AS api_projects_select;
