-- Run with the database owner/migration role, not the API role.
-- Passwords come from environment variables when run by psql/docker init.

\getenv morrus_app_password MORRUS_APP_PASSWORD
\getenv morrus_system_password MORRUS_SYSTEM_PASSWORD
\getenv morrus_migration_password MORRUS_MIGRATION_PASSWORD
\if :{?morrus_app_password}
\else
\set morrus_app_password password123
\endif
\if :{?morrus_system_password}
\else
\set morrus_system_password password123
\endif
\if :{?morrus_migration_password}
\else
\set morrus_migration_password password123
\endif

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'morrus_app') THEN
        CREATE ROLE morrus_app NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
    ELSE
        ALTER ROLE morrus_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'morrus_system') THEN
        CREATE ROLE morrus_system NOSUPERUSER NOCREATEDB NOCREATEROLE BYPASSRLS;
    ELSE
        ALTER ROLE morrus_system LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE BYPASSRLS;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'morrus_migration') THEN
        CREATE ROLE morrus_migration NOSUPERUSER NOCREATEDB NOCREATEROLE BYPASSRLS;
    ELSE
        ALTER ROLE morrus_migration LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE BYPASSRLS;
    END IF;
END
$$;

SELECT format('ALTER ROLE morrus_app LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS', :'morrus_app_password') \gexec
SELECT format('ALTER ROLE morrus_system LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE BYPASSRLS', :'morrus_system_password') \gexec
SELECT format('ALTER ROLE morrus_migration LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE BYPASSRLS', :'morrus_migration_password') \gexec

-- Migration role owns schema objects but is never used by request handling.
SELECT format('ALTER DATABASE %I OWNER TO morrus_migration', current_database()) \gexec
ALTER SCHEMA public OWNER TO morrus_migration;
DO $ownership$
DECLARE r record;
BEGIN
    FOR r IN
        SELECT n.nspname, c.relname, c.relkind
        FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
        WHERE n.nspname='public' AND c.relkind IN ('r','S','v','m')
    LOOP
        EXECUTE format(
            'ALTER %s %I.%I OWNER TO morrus_migration',
            CASE r.relkind WHEN 'S' THEN 'SEQUENCE' WHEN 'v' THEN 'VIEW'
                WHEN 'm' THEN 'MATERIALIZED VIEW' ELSE 'TABLE' END,
            r.nspname,r.relname);
    END LOOP;

    FOR r IN
        SELECT p.oid::regprocedure AS signature
        FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
        WHERE n.nspname='public' AND p.prokind='f'
          AND p.proname IN ('fn_update_inventory_stock','app_current_business_id','app_prevent_business_change')
    LOOP
        EXECUTE format('ALTER FUNCTION %s OWNER TO morrus_migration',r.signature);
    END LOOP;
END
$ownership$;

GRANT USAGE ON SCHEMA public TO morrus_app, morrus_system;
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM morrus_app, morrus_system;

-- Normal runtime role: RLS is mandatory on every tenant table.
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO morrus_app;
DO $grants$
BEGIN
    -- On a fresh docker volume tables do not exist yet; the migration repeats
    -- these grants after creating the schema.
    IF to_regclass('public.businesses') IS NOT NULL THEN
        REVOKE ALL ON businesses FROM morrus_app;
        GRANT SELECT, UPDATE ON businesses TO morrus_app;
        REVOKE ALL ON roles, permissions, role_permissions, refresh_tokens FROM morrus_app;
        GRANT SELECT ON roles, permissions, role_permissions TO morrus_app;

        -- Cross-tenant system role is deliberately narrow: auth/provisioning,
        -- public directory and GoBiz webhook resolution only.
        GRANT SELECT ON businesses, outlets, users, roles, permissions, role_permissions,
            refresh_tokens, gobiz_direct_integrations, gobiz_order_inboxes,
            gobiz_client_configs TO morrus_system;
        GRANT INSERT, UPDATE ON businesses, outlets, users, refresh_tokens TO morrus_system;
        GRANT UPDATE ON gobiz_direct_integrations, gobiz_order_inboxes TO morrus_system;
    END IF;
END
$grants$;
DO $$
BEGIN
    IF to_regclass('public."__EFMigrationsHistory"') IS NOT NULL THEN
        REVOKE ALL ON TABLE "__EFMigrationsHistory" FROM morrus_app;
    END IF;
END
$$;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO morrus_app, morrus_system;

ALTER DEFAULT PRIVILEGES FOR ROLE morrus_migration IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO morrus_app;
ALTER DEFAULT PRIVILEGES FOR ROLE morrus_migration IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO morrus_app;
