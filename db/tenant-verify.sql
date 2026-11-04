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

    IF to_regclass('public.product_serials') IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname='public' AND tablename='product_serials'
          AND indexdef LIKE 'CREATE UNIQUE INDEX%(' || '"BusinessId", "NormalizedSerialNumber"' || ')%')
    THEN
        RAISE EXCEPTION 'Tracking verification failed: business-scoped serial uniqueness is missing';
    END IF;

    IF to_regclass('public.product_serials') IS NOT NULL AND EXISTS (
        SELECT 1 FROM products
        WHERE "TrackingMode" NOT IN ('none','quantity','batch','serial'))
    THEN
        RAISE EXCEPTION 'Tracking verification failed: invalid product tracking mode';
    END IF;

    IF to_regclass('public.product_serials') IS NOT NULL AND EXISTS (
        SELECT 1
        FROM products product
        JOIN inventory_stock stock
          ON stock."BusinessId"=product."BusinessId" AND stock."ProductId"=product."Id"
        WHERE product."TrackingMode"='none' AND stock."QtyOnHand"<>0)
    THEN
        RAISE EXCEPTION 'Tracking verification failed: untracked product has non-zero inventory';
    END IF;

    IF to_regclass('public.product_serials') IS NOT NULL AND EXISTS (
        SELECT 1
        FROM inventory_stock stock
        JOIN products product
          ON product."BusinessId"=stock."BusinessId" AND product."Id"=stock."ProductId"
        LEFT JOIN LATERAL (
            SELECT count(*)::numeric AS qty
            FROM product_serials serial
            WHERE serial."BusinessId"=stock."BusinessId"
              AND serial."ProductId"=stock."ProductId"
              AND serial."OutletId"=stock."OutletId"
              AND serial."ProductVariantId" IS NOT DISTINCT FROM stock."ProductVariantId"
              AND serial."Status"='available'
        ) serial_count ON true
        WHERE product."TrackingMode"='serial' AND stock."QtyOnHand"<>serial_count.qty)
    THEN
        RAISE EXCEPTION 'Tracking verification failed: serial availability does not match aggregate inventory';
    END IF;

    IF to_regclass('public.product_serials') IS NOT NULL AND EXISTS (
        SELECT 1
        FROM inventory_stock stock
        JOIN products product
          ON product."BusinessId"=stock."BusinessId" AND product."Id"=stock."ProductId"
        LEFT JOIN LATERAL (
            SELECT coalesce(sum(batch."QtyRemaining"),0) AS qty
            FROM "ProductBatches" batch
            WHERE batch."BusinessId"=stock."BusinessId"
              AND batch."ProductId"=stock."ProductId"
              AND batch."OutletId"=stock."OutletId"
              AND batch."ProductVariantId" IS NOT DISTINCT FROM stock."ProductVariantId"
        ) batch_total ON true
        WHERE product."TrackingMode"='batch' AND stock."QtyOnHand"<>batch_total.qty)
    THEN
        RAISE EXCEPTION 'Tracking verification failed: batch quantities do not match aggregate inventory';
    END IF;

    IF to_regclass('public.stock_reconciliation_runs') IS NOT NULL AND (
        NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname='trg_prevent_stock_ledger_mutation' AND NOT tgisinternal) OR
        NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname='trg_guard_inventory_stock_projection' AND NOT tgisinternal))
    THEN
        RAISE EXCEPTION 'Stock integrity verification failed: immutable ledger or projection guard trigger is missing';
    END IF;

    IF to_regclass('public.stock_reconciliation_runs') IS NOT NULL AND (
        has_table_privilege('morrus_app','public.stock_ledger','UPDATE') OR
        has_table_privilege('morrus_app','public.stock_ledger','DELETE'))
    THEN
        RAISE EXCEPTION 'Stock integrity verification failed: runtime role can mutate stock ledger';
    END IF;

    IF to_regclass('public.stock_reconciliation_runs') IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
        WHERE n.nspname='public' AND p.proname='fn_reconcile_inventory_stock'
          AND p.prosecdef
          AND EXISTS (SELECT 1 FROM unnest(coalesce(p.proconfig,ARRAY[]::text[])) c
                      WHERE c='search_path=pg_catalog, public'))
    THEN
        RAISE EXCEPTION 'Stock integrity verification failed: reconciliation function is not secured';
    END IF;

    IF to_regclass('public.stock_reconciliation_runs') IS NOT NULL AND EXISTS (
        WITH ledger AS (
            SELECT "BusinessId","OutletId","ProductId","ProductVariantId",sum("QtyChange") qty
            FROM stock_ledger GROUP BY "BusinessId","OutletId","ProductId","ProductVariantId")
        SELECT 1 FROM inventory_stock s
        JOIN products p ON p."BusinessId"=s."BusinessId" AND p."Id"=s."ProductId"
        LEFT JOIN ledger l ON l."BusinessId"=s."BusinessId" AND l."OutletId"=s."OutletId"
          AND l."ProductId"=s."ProductId"
          AND l."ProductVariantId" IS NOT DISTINCT FROM s."ProductVariantId"
        WHERE p."TrackingMode"<>'none' AND s."QtyOnHand"<>coalesce(l.qty,0))
    THEN
        RAISE EXCEPTION 'Stock integrity verification failed: inventory projection differs from ledger';
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
