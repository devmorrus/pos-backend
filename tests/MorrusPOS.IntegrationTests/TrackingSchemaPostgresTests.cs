using Npgsql;

namespace MorrusPOS.IntegrationTests;

public class TrackingSchemaPostgresTests
{
    [Fact]
    public async Task TrackingSchema_HasPostgresConstraintsAndTenantSafeTransferReferences()
    {
        var connectionString = IntegrationTestDatabase.RequireConnectionString("MORRUS_TENANCY_TEST_ADMIN");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
              to_regclass('public.product_serials') IS NOT NULL
              AND EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conname='CK_products_TrackingMode' AND contype='c')
              AND EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conname='CK_product_serials_Status' AND contype='c')
              AND EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE schemaname='public' AND tablename='product_serials'
                  AND indexdef LIKE 'CREATE UNIQUE INDEX%"BusinessId", "NormalizedSerialNumber"%')
              AND EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='stock_transfer_items' AND column_name='ProductBatchId')
              AND EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='stock_transfer_items' AND column_name='ProductSerialId')
              AND EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conname='FKT_stock_transfer_items_batch' AND contype='f')
              AND EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conname='FKT_stock_transfer_items_serial' AND contype='f')
              AND to_regclass('public.stock_reconciliation_runs') IS NOT NULL
              AND EXISTS (
                SELECT 1 FROM pg_trigger
                WHERE tgname='trg_prevent_stock_ledger_mutation' AND NOT tgisinternal)
              AND EXISTS (
                SELECT 1 FROM pg_trigger
                WHERE tgname='trg_guard_inventory_stock_projection' AND NOT tgisinternal)
              AND EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE schemaname='public' AND tablename='stock_ledger'
                  AND indexdef LIKE 'CREATE UNIQUE INDEX%"BusinessId", "PostingKey"%')
              AND EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='UX_payments_Business_Method_Reference')
              AND EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='UX_supplier_payments_Business_Method_Reference');
            """;

        Assert.True((bool)(await command.ExecuteScalarAsync())!);
    }
}
