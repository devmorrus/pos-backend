using Npgsql;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;

namespace MorrusPOS.IntegrationTests;

public class StockLedgerPostgresTests
{
    [Fact]
    public async Task LedgerTrigger_Immutability_AndReconciliation_WorkTogether()
    {
        var adminConnection = IntegrationTestDatabase.RequireConnectionString("MORRUS_TENANCY_TEST_ADMIN");
        var appConnection = IntegrationTestDatabase.RequireConnectionString("MORRUS_TENANCY_TEST_APP");

        var businessId = Guid.NewGuid();
        var outletId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var ledgerId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..10];

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        try
        {
            await using (var seed = admin.CreateCommand())
            {
                seed.CommandText = """
                    INSERT INTO businesses
                        ("Id","Name","Slug","Category","IndustryProfileId","SubscriptionStatus","TrialStartDate","TrialEndDate","CreatedAt","UpdatedAt")
                    VALUES (@business,'Stock Test',@slug,'Test','ca100001-0000-4000-8000-000000000001','Active',now(),now()+interval '1 day',now(),now());
                    INSERT INTO outlets ("Id","BusinessId","Name","Code","IsActive","CreatedAt","UpdatedAt")
                    VALUES (@outlet,@business,'Stock Outlet',@code,true,now(),now());
                    INSERT INTO stock_locations
                        ("Id","BusinessId","Type","Code","Name","OutletId","IsActive","CreatedAt","UpdatedAt")
                    VALUES (@outlet,@business,'outlet',@code,'Stock Outlet',@outlet,true,now(),now());
                    INSERT INTO categories ("Id","BusinessId","Name","ParentId","CreatedAt","UpdatedAt")
                    VALUES (@category,@business,'Stock Category',NULL,now(),now());
                    INSERT INTO products
                        ("Id","BusinessId","CategoryId","Sku","Name","BasePrice","CostPrice","Unit",
                         "IsConsignment","HasVariants","IsRawMaterial","IsActive","TrackingMode","CreatedAt","UpdatedAt")
                    VALUES
                        (@product,@business,@category,@sku,'Stock Product',100,50,'pcs',false,false,false,true,'quantity',now(),now());
                    """;
                seed.Parameters.AddWithValue("business", businessId);
                seed.Parameters.AddWithValue("outlet", outletId);
                seed.Parameters.AddWithValue("category", categoryId);
                seed.Parameters.AddWithValue("product", productId);
                seed.Parameters.AddWithValue("slug", $"stock-test-{suffix}");
                seed.Parameters.AddWithValue("code", $"STK-{suffix}");
                seed.Parameters.AddWithValue("sku", $"SKU-{suffix}");
                await seed.ExecuteNonQueryAsync();
            }

            await using var app = new NpgsqlConnection(appConnection);
            await app.OpenAsync();
            await SetBusinessAsync(app, businessId);

            await using (var movement = app.CreateCommand())
            {
                movement.CommandText = """
                    INSERT INTO stock_ledger
                        ("Id","BusinessId","ProductId","ProductVariantId","StockLocationId","ProductBatchId","ProductSerialId",
                         "DocumentItemId","PostingKey","IsSystemGenerated","MovementType","QtyChange","ReferenceType",
                         "ReferenceId","Note","CreatedBy","CreatedAt")
                    VALUES (@id,@business,@product,NULL,@outlet,NULL,NULL,NULL,@key,false,'purchase_in',5,'integration_test',@reference,NULL,NULL,now());
                    """;
                movement.Parameters.AddWithValue("id", ledgerId);
                movement.Parameters.AddWithValue("business", businessId);
                movement.Parameters.AddWithValue("product", productId);
                movement.Parameters.AddWithValue("outlet", outletId);
                movement.Parameters.AddWithValue("key", $"integration/{ledgerId:N}");
                movement.Parameters.AddWithValue("reference", referenceId);
                await movement.ExecuteNonQueryAsync();
            }

            Assert.Equal(5m, await ReadBalanceAsync(app, productId, outletId));

            await using (var directBalanceWrite = app.CreateCommand())
            {
                directBalanceWrite.CommandText = "UPDATE inventory_stock SET \"QtyOnHand\"=4 WHERE \"ProductId\"=@product AND \"StockLocationId\"=@outlet;";
                directBalanceWrite.Parameters.AddWithValue("product", productId);
                directBalanceWrite.Parameters.AddWithValue("outlet", outletId);
                var exception = await Assert.ThrowsAsync<PostgresException>(() => directBalanceWrite.ExecuteNonQueryAsync());
                Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
            }
            Assert.Equal(5m, await ReadBalanceAsync(app, productId, outletId));

            await using (var immutable = app.CreateCommand())
            {
                immutable.CommandText = "UPDATE stock_ledger SET \"Note\"='changed' WHERE \"Id\"=@id;";
                immutable.Parameters.AddWithValue("id", ledgerId);
                var exception = await Assert.ThrowsAsync<PostgresException>(() => immutable.ExecuteNonQueryAsync());
                Assert.Contains(exception.SqlState, new[] { PostgresErrorCodes.InsufficientPrivilege, PostgresErrorCodes.RaiseException });
            }

            await using (var corrupt = admin.CreateCommand())
            {
                corrupt.CommandText = """
                    BEGIN;
                    SET LOCAL session_replication_role = replica;
                    UPDATE inventory_stock SET "QtyOnHand"=2 WHERE "BusinessId"=@business AND "ProductId"=@product AND "StockLocationId"=@outlet;
                    COMMIT;
                    """;
                corrupt.Parameters.AddWithValue("business", businessId);
                corrupt.Parameters.AddWithValue("product", productId);
                corrupt.Parameters.AddWithValue("outlet", outletId);
                await corrupt.ExecuteNonQueryAsync();
            }

            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(app).Options;
            await using (var db = new AppDbContext(options))
            {
                var preview = await db.Database
                    .SqlQuery<StockReconciliationSqlRow>($"SELECT * FROM fn_reconcile_inventory_stock({outletId}, {false})")
                    .ToListAsync();
                Assert.Single(preview);
                Assert.Equal(3m, preview[0].Difference);
                Assert.True(preview[0].Repairable);
            }
            await using (var repair = app.CreateCommand())
            {
                repair.CommandText = "SELECT count(*) FROM fn_reconcile_inventory_stock(@outlet,true);";
                repair.Parameters.AddWithValue("outlet", outletId);
                Assert.Equal(1L, (long)(await repair.ExecuteScalarAsync())!);
            }

            Assert.Equal(5m, await ReadBalanceAsync(app, productId, outletId));
            await using var countLedger = app.CreateCommand();
            countLedger.CommandText = "SELECT count(*) FROM stock_ledger WHERE \"Id\"=@id;";
            countLedger.Parameters.AddWithValue("id", ledgerId);
            Assert.Equal(1L, (long)(await countLedger.ExecuteScalarAsync())!);
        }
        finally
        {
            await using var cleanup = admin.CreateCommand();
            cleanup.CommandText = """
                BEGIN;
                SET LOCAL session_replication_role = replica;
                DELETE FROM stock_ledger WHERE "BusinessId"=@business;
                DELETE FROM inventory_stock WHERE "BusinessId"=@business;
                DELETE FROM products WHERE "BusinessId"=@business;
                DELETE FROM categories WHERE "BusinessId"=@business;
                DELETE FROM stock_locations WHERE "BusinessId"=@business;
                DELETE FROM outlets WHERE "BusinessId"=@business;
                DELETE FROM businesses WHERE "Id"=@business;
                COMMIT;
                """;
            cleanup.Parameters.AddWithValue("business", businessId);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static async Task SetBusinessAsync(NpgsqlConnection connection, Guid businessId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select set_config('app.business_id',@business,false);";
        command.Parameters.AddWithValue("business", businessId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<decimal> ReadBalanceAsync(NpgsqlConnection connection, Guid productId, Guid outletId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"QtyOnHand\" FROM inventory_stock WHERE \"ProductId\"=@product AND \"StockLocationId\"=@outlet;";
        command.Parameters.AddWithValue("product", productId);
        command.Parameters.AddWithValue("outlet", outletId);
        return (decimal)(await command.ExecuteScalarAsync())!;
    }
}
