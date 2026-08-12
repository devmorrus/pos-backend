using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations;

/// <summary>
/// Recreates the stock ledger trigger so production databases that still have
/// an older snake_case trigger body are brought back in sync with the EF schema.
/// </summary>
public partial class FixInventoryStockTrigger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS trg_stock_ledger_insert ON "stock_ledger";
            DROP FUNCTION IF EXISTS fn_update_inventory_stock();

            CREATE OR REPLACE FUNCTION fn_update_inventory_stock()
            RETURNS TRIGGER AS $$
            BEGIN
                INSERT INTO "inventory_stock" ("Id", "ProductId", "OutletId", "QtyOnHand", "MinStockAlert", "UpdatedAt")
                VALUES (gen_random_uuid(), NEW."ProductId", NEW."OutletId", NEW."QtyChange", 0, now())
                ON CONFLICT ("ProductId", "OutletId")
                DO UPDATE SET
                    "QtyOnHand" = "inventory_stock"."QtyOnHand" + NEW."QtyChange",
                    "UpdatedAt" = now();

                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER trg_stock_ledger_insert
            AFTER INSERT ON "stock_ledger"
            FOR EACH ROW
            EXECUTE FUNCTION fn_update_inventory_stock();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS trg_stock_ledger_insert ON "stock_ledger";
            DROP FUNCTION IF EXISTS fn_update_inventory_stock();
            """);
    }
}
