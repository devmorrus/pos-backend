using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixNegativeStockLedgerProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FixedProjectionFunction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PreviousProjectionFunction);
        }

        private const string FixedProjectionFunction = """
            CREATE OR REPLACE FUNCTION fn_update_inventory_stock()
            RETURNS trigger
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path=pg_catalog,public
            AS $fn$
            BEGIN
                PERFORM pg_advisory_xact_lock_shared(hashtextextended(NEW."BusinessId"::text || ':' || NEW."OutletId"::text,0));
                IF NOT EXISTS (
                    SELECT 1 FROM public.products p
                    WHERE p."BusinessId"=NEW."BusinessId" AND p."Id"=NEW."ProductId"
                      AND p."TrackingMode"<>'none')
                THEN
                    RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: produk tidak valid atau tidak memiliki stok';
                END IF;

                IF NEW."ProductVariantId" IS NOT NULL THEN
                    UPDATE public.inventory_stock
                    SET "QtyOnHand"="QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now()
                    WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId"
                      AND "ProductVariantId"=NEW."ProductVariantId" AND "OutletId"=NEW."OutletId"
                      AND "QtyOnHand"+NEW."QtyChange">=0;
                    IF FOUND THEN RETURN NEW; END IF;

                    IF EXISTS (
                        SELECT 1 FROM public.inventory_stock
                        WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId"
                          AND "ProductVariantId"=NEW."ProductVariantId" AND "OutletId"=NEW."OutletId")
                    THEN
                        RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: stok tidak mencukupi';
                    END IF;
                ELSE
                    UPDATE public.inventory_stock
                    SET "QtyOnHand"="QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now()
                    WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId"
                      AND "ProductVariantId" IS NULL AND "OutletId"=NEW."OutletId"
                      AND "QtyOnHand"+NEW."QtyChange">=0;
                    IF FOUND THEN RETURN NEW; END IF;

                    IF EXISTS (
                        SELECT 1 FROM public.inventory_stock
                        WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId"
                          AND "ProductVariantId" IS NULL AND "OutletId"=NEW."OutletId")
                    THEN
                        RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: stok tidak mencukupi';
                    END IF;
                END IF;

                IF NEW."QtyChange"<0 THEN
                    RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: stok tidak mencukupi';
                END IF;

                IF NEW."ProductVariantId" IS NOT NULL THEN
                    INSERT INTO public.inventory_stock
                        ("Id","BusinessId","ProductId","ProductVariantId","OutletId","QtyOnHand","MinStockAlert","UpdatedAt")
                    VALUES
                        (gen_random_uuid(),NEW."BusinessId",NEW."ProductId",NEW."ProductVariantId",NEW."OutletId",NEW."QtyChange",0,now())
                    ON CONFLICT ("BusinessId","ProductId","ProductVariantId","OutletId")
                        WHERE "ProductVariantId" IS NOT NULL
                    DO UPDATE SET "QtyOnHand"=public.inventory_stock."QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now();
                ELSE
                    INSERT INTO public.inventory_stock
                        ("Id","BusinessId","ProductId","OutletId","QtyOnHand","MinStockAlert","UpdatedAt")
                    VALUES
                        (gen_random_uuid(),NEW."BusinessId",NEW."ProductId",NEW."OutletId",NEW."QtyChange",0,now())
                    ON CONFLICT ("BusinessId","ProductId","OutletId") WHERE "ProductVariantId" IS NULL
                    DO UPDATE SET "QtyOnHand"=public.inventory_stock."QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now();
                END IF;
                RETURN NEW;
            END
            $fn$;
            """;

        private const string PreviousProjectionFunction = """
            CREATE OR REPLACE FUNCTION fn_update_inventory_stock()
            RETURNS trigger
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path=pg_catalog,public
            AS $fn$
            BEGIN
                PERFORM pg_advisory_xact_lock_shared(hashtextextended(NEW."BusinessId"::text || ':' || NEW."OutletId"::text,0));
                IF NOT EXISTS (
                    SELECT 1 FROM public.products p
                    WHERE p."BusinessId"=NEW."BusinessId" AND p."Id"=NEW."ProductId"
                      AND p."TrackingMode"<>'none')
                THEN
                    RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: produk tidak valid atau tidak memiliki stok';
                END IF;
                IF NEW."ProductVariantId" IS NOT NULL THEN
                    INSERT INTO public.inventory_stock
                        ("Id","BusinessId","ProductId","ProductVariantId","OutletId","QtyOnHand","MinStockAlert","UpdatedAt")
                    VALUES
                        (gen_random_uuid(),NEW."BusinessId",NEW."ProductId",NEW."ProductVariantId",NEW."OutletId",NEW."QtyChange",0,now())
                    ON CONFLICT ("BusinessId","ProductId","ProductVariantId","OutletId")
                        WHERE "ProductVariantId" IS NOT NULL
                    DO UPDATE SET "QtyOnHand"=public.inventory_stock."QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now();
                ELSE
                    INSERT INTO public.inventory_stock
                        ("Id","BusinessId","ProductId","OutletId","QtyOnHand","MinStockAlert","UpdatedAt")
                    VALUES
                        (gen_random_uuid(),NEW."BusinessId",NEW."ProductId",NEW."OutletId",NEW."QtyChange",0,now())
                    ON CONFLICT ("BusinessId","ProductId","OutletId") WHERE "ProductVariantId" IS NULL
                    DO UPDATE SET "QtyOnHand"=public.inventory_stock."QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now();
                END IF;
                RETURN NEW;
            END
            $fn$;
            """;
    }
}
