\set ON_ERROR_STOP on

SELECT jsonb_build_object(
    'backup_id', :'backup_id',
    'backup_reason', :'backup_reason',
    'started_at_utc', :'started_at_utc',
    'completed_at_utc', :'completed_at_utc',
    'source_host', :'source_host',
    'git_commit', :'git_commit',
    'service_version', :'service_version',
    'postgres_server_version', current_setting('server_version'),
    'postgres_client_version', :'postgres_client_version',
    'database_name', current_database(),
    'database_size_bytes', pg_database_size(current_database()),
    'dump_size_bytes', :'dump_size_bytes'::bigint,
    'dump_sha256', :'dump_sha256',
    'latest_ef_migration', (SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1),
    'migration_count', (SELECT count(*) FROM "__EFMigrationsHistory"),
    'upload_file_count', :'upload_file_count'::bigint,
    'upload_size_bytes', :'upload_size_bytes'::bigint,
    'critical_table_counts', jsonb_build_object(
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
    )
)::text;
