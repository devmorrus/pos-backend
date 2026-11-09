\set ON_ERROR_STOP on

CREATE TEMP TABLE restore_manifest(data jsonb);
INSERT INTO restore_manifest(data) VALUES (:'manifest_json'::jsonb);

DO $verify$
DECLARE
    manifest jsonb;
    actual_counts jsonb;
    expected_counts jsonb;
    expected_migration text;
    actual_migration text;
BEGIN
    SELECT data INTO manifest FROM restore_manifest;
    expected_counts := manifest->'critical_table_counts';
    expected_migration := manifest->>'latest_ef_migration';

    SELECT "MigrationId" INTO actual_migration
    FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;

    IF actual_migration IS DISTINCT FROM expected_migration THEN
        RAISE EXCEPTION 'Restore verification failed: latest migration is %, expected %',
            actual_migration, expected_migration;
    END IF;

    actual_counts := jsonb_build_object(
        'businesses', (SELECT count(*) FROM businesses),
        'outlets', (SELECT count(*) FROM outlets),
        'users', (SELECT count(*) FROM users),
        'products', (SELECT count(*) FROM products),
        'product_batches', (SELECT count(*) FROM "ProductBatches"),
        'product_serials', (SELECT count(*) FROM product_serials),
        'inventory_stock', (SELECT count(*) FROM inventory_stock),
        'stock_ledger', (SELECT count(*) FROM stock_ledger),
        'transactions', (SELECT count(*) FROM transactions),
        'transaction_items', (SELECT count(*) FROM transaction_items),
        'payments', (SELECT count(*) FROM payments),
        'cash_flows', (SELECT count(*) FROM cash_flows),
        'idempotency_records', (SELECT count(*) FROM idempotency_records),
        'journal_entries', (SELECT count(*) FROM journal_entries),
        'account_transactions', (SELECT count(*) FROM account_transactions),
        'gobiz_order_inboxes', (SELECT count(*) FROM gobiz_order_inboxes)
    );

    IF actual_counts IS DISTINCT FROM expected_counts THEN
        RAISE EXCEPTION 'Restore verification failed: critical table counts differ. Actual=%, Expected=%',
            actual_counts, expected_counts;
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_index i
        JOIN pg_class c ON c.oid=i.indexrelid
        JOIN pg_namespace n ON n.oid=c.relnamespace
        WHERE n.nspname='public' AND (NOT i.indisvalid OR NOT i.indisready))
    THEN
        RAISE EXCEPTION 'Restore verification failed: invalid or unready index exists';
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_constraint c
        JOIN pg_namespace n ON n.oid=c.connamespace
        WHERE n.nspname='public' AND NOT c.convalidated)
    THEN
        RAISE EXCEPTION 'Restore verification failed: unvalidated constraint exists';
    END IF;

    IF EXISTS (SELECT 1 FROM businesses WHERE "OwnerId" IS NULL) OR
       EXISTS (SELECT 1 FROM outlets WHERE "BusinessId" IS NULL) OR
       EXISTS (SELECT 1 FROM users WHERE "BusinessId" IS NULL) OR
       EXISTS (SELECT 1 FROM products WHERE "BusinessId" IS NULL) OR
       EXISTS (SELECT 1 FROM categories WHERE "BusinessId" IS NULL) OR
       EXISTS (SELECT 1 FROM suppliers WHERE "BusinessId" IS NULL)
    THEN
        RAISE EXCEPTION 'Restore verification failed: required tenant ownership is missing';
    END IF;

    IF EXISTS (
        SELECT 1 FROM products p
        JOIN categories c ON c."Id"=p."CategoryId"
        WHERE p."BusinessId"<>c."BusinessId")
    THEN
        RAISE EXCEPTION 'Restore verification failed: cross-tenant product category exists';
    END IF;
END
$verify$;
