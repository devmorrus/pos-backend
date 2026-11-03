-- Post-deployment verification. Exits with an error on incomplete isolation.
\set ON_ERROR_STOP on

DO $verify$
DECLARE
    r record;
    remaining bigint;
BEGIN
    FOR r IN
        SELECT n.nspname, c.relname, c.relrowsecurity, c.relforcerowsecurity
        FROM pg_class c
        JOIN pg_namespace n ON n.oid=c.relnamespace
        WHERE c.relkind='r' AND n.nspname='public'
          AND EXISTS (
              SELECT 1 FROM pg_attribute a
              WHERE a.attrelid=c.oid AND a.attname='BusinessId' AND NOT a.attisdropped)
    LOOP
        EXECUTE format('SELECT count(*) FROM %I.%I WHERE "BusinessId" IS NULL',r.nspname,r.relname)
            INTO remaining;
        IF remaining > 0 THEN
            RAISE EXCEPTION 'Tenant verification failed: %.% has % NULL BusinessId rows',
                r.nspname,r.relname,remaining;
        END IF;
        IF NOT r.relrowsecurity OR NOT r.relforcerowsecurity THEN
            RAISE EXCEPTION 'Tenant verification failed: RLS/FORCE RLS missing on %.%',
                r.nspname,r.relname;
        END IF;
    END LOOP;

    IF NOT EXISTS (
        SELECT 1 FROM pg_roles
        WHERE rolname='morrus_app' AND NOT rolsuper AND NOT rolbypassrls)
    THEN
        RAISE EXCEPTION 'Tenant verification failed: morrus_app must be non-superuser and NOBYPASSRLS';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_roles
        WHERE rolname='morrus_system' AND NOT rolsuper AND rolbypassrls)
    THEN
        RAISE EXCEPTION 'Tenant verification failed: morrus_system role is invalid';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_roles
        WHERE rolname='morrus_migration' AND NOT rolsuper AND rolbypassrls)
    THEN
        RAISE EXCEPTION 'Tenant verification failed: morrus_migration role is invalid';
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
        WHERE n.nspname='public' AND c.relkind='r'
          AND pg_get_userbyid(c.relowner)<>'morrus_migration')
    THEN
        RAISE EXCEPTION 'Tenant verification failed: public tables must be owned by morrus_migration';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_class c
        WHERE c.oid='public.businesses'::regclass
          AND c.relrowsecurity AND c.relforcerowsecurity
          AND EXISTS (SELECT 1 FROM pg_policy p WHERE p.polrelid=c.oid AND p.polname='tenant_isolation'))
    THEN
        RAISE EXCEPTION 'Tenant verification failed: businesses RLS policy is missing';
    END IF;

    IF to_regclass('public.capabilities') IS NOT NULL AND
       (SELECT count(*) FROM capabilities WHERE "IsActive") < 27
    THEN
        RAISE EXCEPTION 'Capability verification failed: capability catalog is incomplete';
    END IF;

    IF to_regclass('public.industry_profiles') IS NOT NULL AND
       (SELECT count(*) FROM industry_profiles WHERE "IsActive") < 7
    THEN
        RAISE EXCEPTION 'Capability verification failed: industry profile catalog is incomplete';
    END IF;

    IF to_regclass('public.industry_profiles') IS NOT NULL AND EXISTS (
        SELECT 1
        FROM (VALUES ('electronics'), ('food-beverage'), ('retail')) AS required(code)
        LEFT JOIN industry_profiles profile
          ON profile."Code" = required.code AND profile."IsActive"
        WHERE profile."Id" IS NULL)
    THEN
        RAISE EXCEPTION 'Capability verification failed: required ready-to-use industry profile is missing';
    END IF;

    IF to_regclass('public.industry_profiles') IS NOT NULL AND EXISTS (
        SELECT 1
        FROM industry_profiles
        GROUP BY "Code"
        HAVING count(*) > 1)
    THEN
        RAISE EXCEPTION 'Capability verification failed: duplicate industry profile code';
    END IF;

    IF to_regclass('public.industry_profile_capabilities') IS NOT NULL AND EXISTS (
        SELECT 1
        FROM industry_profiles profile
        WHERE profile."IsActive"
          AND (SELECT count(*)
               FROM industry_profile_capabilities mapping
               JOIN capabilities capability ON capability."Id" = mapping."CapabilityId"
               WHERE mapping."IndustryProfileId" = profile."Id" AND capability."IsActive")
              <> (SELECT count(*) FROM capabilities WHERE "IsActive"))
    THEN
        RAISE EXCEPTION 'Capability verification failed: industry profile capability mapping is incomplete';
    END IF;

    IF to_regclass('public.capabilities') IS NOT NULL AND (
        has_table_privilege('morrus_app','public.capabilities','INSERT') OR
        has_table_privilege('morrus_app','public.capabilities','UPDATE') OR
        has_table_privilege('morrus_app','public.capabilities','DELETE'))
    THEN
        RAISE EXCEPTION 'Capability verification failed: morrus_app may only read global capability catalog';
    END IF;

    IF to_regclass('public.capability_change_logs') IS NOT NULL AND (
        has_table_privilege('morrus_app','public.capability_change_logs','UPDATE') OR
        has_table_privilege('morrus_app','public.capability_change_logs','DELETE'))
    THEN
        RAISE EXCEPTION 'Capability verification failed: capability audit log must be append-only';
    END IF;
END
$verify$;

SELECT
    count(*) AS tenant_tables,
    count(*) FILTER (WHERE c.relrowsecurity AND c.relforcerowsecurity) AS fully_rls_protected
FROM pg_class c
JOIN pg_namespace n ON n.oid=c.relnamespace
WHERE c.relkind='r' AND n.nspname='public'
  AND EXISTS (
      SELECT 1 FROM pg_attribute a
      WHERE a.attrelid=c.oid AND a.attname='BusinessId' AND NOT a.attisdropped);

SELECT count(*) AS composite_tenant_foreign_keys
FROM pg_constraint
WHERE conname LIKE 'FKT_%';
