using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStockIndexesAndTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inventory_stock_ProductId_OutletId",
                table: "inventory_stock");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stock_ProductId_OutletId",
                table: "inventory_stock",
                columns: new[] { "ProductId", "OutletId" },
                unique: true,
                filter: "\"ProductVariantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stock_ProductId_ProductVariantId_OutletId",
                table: "inventory_stock",
                columns: new[] { "ProductId", "ProductVariantId", "OutletId" },
                unique: true,
                filter: "\"ProductVariantId\" IS NOT NULL");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION fn_update_inventory_stock()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF NEW.""ProductVariantId"" IS NOT NULL THEN
                        INSERT INTO ""inventory_stock"" (""Id"", ""ProductId"", ""ProductVariantId"", ""OutletId"", ""QtyOnHand"", ""MinStockAlert"", ""UpdatedAt"")
                        VALUES (gen_random_uuid(), NEW.""ProductId"", NEW.""ProductVariantId"", NEW.""OutletId"", NEW.""QtyChange"", 0, now())
                        ON CONFLICT (""ProductId"", ""ProductVariantId"", ""OutletId"")
                        DO UPDATE SET
                            ""QtyOnHand"" = ""inventory_stock"".""QtyOnHand"" + NEW.""QtyChange"",
                            ""UpdatedAt"" = now();
                    ELSE
                        INSERT INTO ""inventory_stock"" (""Id"", ""ProductId"", ""OutletId"", ""QtyOnHand"", ""MinStockAlert"", ""UpdatedAt"")
                        VALUES (gen_random_uuid(), NEW.""ProductId"", NEW.""OutletId"", NEW.""QtyChange"", 0, now())
                        ON CONFLICT (""ProductId"", ""OutletId"")
                        DO UPDATE SET
                            ""QtyOnHand"" = ""inventory_stock"".""QtyOnHand"" + NEW.""QtyChange"",
                            ""UpdatedAt"" = now();
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inventory_stock_ProductId_OutletId",
                table: "inventory_stock");

            migrationBuilder.DropIndex(
                name: "IX_inventory_stock_ProductId_ProductVariantId_OutletId",
                table: "inventory_stock");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stock_ProductId_OutletId",
                table: "inventory_stock",
                columns: new[] { "ProductId", "OutletId" },
                unique: true);

            migrationBuilder.Sql(@"
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
            ");
        }
    }
}
