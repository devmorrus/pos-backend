using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixStockTriggerConflictTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION fn_update_inventory_stock()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF NEW.""ProductVariantId"" IS NOT NULL THEN
                        INSERT INTO ""inventory_stock"" (""Id"", ""ProductId"", ""ProductVariantId"", ""OutletId"", ""QtyOnHand"", ""MinStockAlert"", ""UpdatedAt"")
                        VALUES (gen_random_uuid(), NEW.""ProductId"", NEW.""ProductVariantId"", NEW.""OutletId"", NEW.""QtyChange"", 0, now())
                        ON CONFLICT (""ProductId"", ""ProductVariantId"", ""OutletId"") WHERE ""ProductVariantId"" IS NOT NULL
                        DO UPDATE SET
                            ""QtyOnHand"" = ""inventory_stock"".""QtyOnHand"" + NEW.""QtyChange"",
                            ""UpdatedAt"" = now();
                    ELSE
                        INSERT INTO ""inventory_stock"" (""Id"", ""ProductId"", ""OutletId"", ""QtyOnHand"", ""MinStockAlert"", ""UpdatedAt"")
                        VALUES (gen_random_uuid(), NEW.""ProductId"", NEW.""OutletId"", NEW.""QtyChange"", 0, now())
                        ON CONFLICT (""ProductId"", ""OutletId"") WHERE ""ProductVariantId"" IS NULL
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
    }
}
