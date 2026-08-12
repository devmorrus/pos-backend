using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MorrusPOS.Infrastructure.Persistence;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260812021500_RecreateStockLedgerTriggerWithQuotedColumns")]
    public partial class RecreateStockLedgerTriggerWithQuotedColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE EXTENSION IF NOT EXISTS pgcrypto;

                CREATE OR REPLACE FUNCTION fn_update_inventory_stock()
                RETURNS TRIGGER AS $$
                BEGIN
                    INSERT INTO ""inventory_stock"" (""Id"", ""ProductId"", ""OutletId"", ""QtyOnHand"", ""MinStockAlert"", ""UpdatedAt"")
                    VALUES (gen_random_uuid(), NEW.""ProductId"", NEW.""OutletId"", NEW.""QtyChange"", 0, now())
                    ON CONFLICT (""ProductId"", ""OutletId"")
                    DO UPDATE SET
                        ""QtyOnHand"" = ""inventory_stock"".""QtyOnHand"" + NEW.""QtyChange"",
                        ""UpdatedAt"" = now();

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS trg_stock_ledger_insert ON ""stock_ledger"";

                CREATE TRIGGER trg_stock_ledger_insert
                AFTER INSERT ON ""stock_ledger""
                FOR EACH ROW
                EXECUTE FUNCTION fn_update_inventory_stock();
                ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS trg_stock_ledger_insert ON ""stock_ledger"";
                DROP FUNCTION IF EXISTS fn_update_inventory_stock();
                ");
        }
    }
}
