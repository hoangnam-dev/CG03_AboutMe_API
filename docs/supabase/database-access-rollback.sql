\set ON_ERROR_STOP on

-- Safe rollback for privileges introduced by database-access.sql. This does not
-- re-expose application data to Supabase Data API roles. If the deployment must
-- restore their prior grants, run the exact GRANT statements captured before the
-- change as described in SPRINT_9_SECURITY_CHECKLIST.md; never substitute GRANT ALL.

BEGIN;

ALTER DEFAULT PRIVILEGES FOR ROLE portfolio_migrator IN SCHEMA public
    REVOKE SELECT, INSERT, UPDATE, DELETE ON TABLES FROM portfolio_api;

REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA public FROM portfolio_api;
REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public FROM portfolio_api;
REVOKE USAGE ON SCHEMA public FROM portfolio_api;

REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA public FROM portfolio_migrator;
REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public FROM portfolio_migrator;
REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA public FROM portfolio_migrator;
REVOKE USAGE, CREATE ON SCHEMA public FROM portfolio_migrator;

COMMIT;

-- Group roles are intentionally retained because platform login-role membership
-- is managed separately. Remove membership first and DROP ROLE manually only when
-- the corresponding runtime/migration credentials have been retired.

