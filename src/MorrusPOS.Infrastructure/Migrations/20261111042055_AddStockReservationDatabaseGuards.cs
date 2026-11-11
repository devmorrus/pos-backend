using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockReservationDatabaseGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_guard_stock_reservation_item()
                RETURNS trigger
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path=pg_catalog,public
                AS $fn$
                DECLARE
                    parent_business uuid;
                    parent_location uuid;
                    parent_status text;
                    parent_expiry timestamptz;
                    physical_qty numeric;
                    reserved_qty numeric;
                    batch_qty numeric;
                    batch_reserved numeric;
                    product_mode text;
                BEGIN
                    IF TG_OP='UPDATE' THEN
                        IF OLD."Status"<>'active' THEN
                            RAISE EXCEPTION 'STOCK_RESERVATION_NOT_ACTIVE: item reservasi terminal tidak boleh diubah';
                        END IF;
                        IF NEW."BusinessId" IS DISTINCT FROM OLD."BusinessId"
                           OR NEW."StockReservationId" IS DISTINCT FROM OLD."StockReservationId"
                           OR NEW."StockLocationId" IS DISTINCT FROM OLD."StockLocationId"
                           OR NEW."ProductId" IS DISTINCT FROM OLD."ProductId"
                           OR NEW."ProductVariantId" IS DISTINCT FROM OLD."ProductVariantId"
                           OR NEW."ProductBatchId" IS DISTINCT FROM OLD."ProductBatchId"
                           OR NEW."ProductSerialId" IS DISTINCT FROM OLD."ProductSerialId"
                           OR NEW."TrackingModeSnapshot" IS DISTINCT FROM OLD."TrackingModeSnapshot"
                           OR NEW."Qty" IS DISTINCT FROM OLD."Qty"
                        THEN
                            RAISE EXCEPTION 'STOCK_RESERVATION_MISMATCH: identitas dan qty item reservasi tidak boleh diubah';
                        END IF;
                        RETURN NEW;
                    END IF;

                    SELECT r."BusinessId",r."StockLocationId",r."Status",r."ExpiresAt"
                    INTO parent_business,parent_location,parent_status,parent_expiry
                    FROM public.stock_reservations r WHERE r."Id"=NEW."StockReservationId";
                    IF parent_business IS NULL OR parent_business<>NEW."BusinessId"
                       OR parent_location<>NEW."StockLocationId"
                       OR parent_status<>'active' OR parent_expiry<=now()
                    THEN
                        RAISE EXCEPTION 'STOCK_RESERVATION_NOT_ACTIVE: induk reservasi tidak valid atau kedaluwarsa';
                    END IF;

                    SELECT p."TrackingMode" INTO product_mode
                    FROM public.products p
                    WHERE p."BusinessId"=NEW."BusinessId" AND p."Id"=NEW."ProductId" AND p."IsActive";
                    IF product_mode IS NULL OR product_mode<>NEW."TrackingModeSnapshot" THEN
                        RAISE EXCEPTION 'STOCK_RESERVATION_MISMATCH: mode tracking produk tidak cocok';
                    END IF;

                    SELECT s."QtyOnHand" INTO physical_qty
                    FROM public.inventory_stock s
                    WHERE s."BusinessId"=NEW."BusinessId"
                      AND s."StockLocationId"=NEW."StockLocationId"
                      AND s."ProductId"=NEW."ProductId"
                      AND s."ProductVariantId" IS NOT DISTINCT FROM NEW."ProductVariantId"
                    FOR UPDATE;
                    reserved_qty := public.fn_active_reserved_qty(
                        NEW."BusinessId",NEW."StockLocationId",NEW."ProductId",NEW."ProductVariantId");
                    IF physical_qty IS NULL OR physical_qty-reserved_qty<NEW."Qty" THEN
                        RAISE EXCEPTION 'STOCK_RESERVATION_INSUFFICIENT: stok tersedia tidak mencukupi';
                    END IF;

                    IF NEW."ProductBatchId" IS NOT NULL THEN
                        SELECT b."QtyRemaining" INTO batch_qty
                        FROM public."ProductBatches" b
                        WHERE b."BusinessId"=NEW."BusinessId" AND b."Id"=NEW."ProductBatchId"
                          AND b."StockLocationId"=NEW."StockLocationId" AND b."ProductId"=NEW."ProductId"
                          AND b."ProductVariantId" IS NOT DISTINCT FROM NEW."ProductVariantId"
                        FOR UPDATE;
                        SELECT coalesce(sum(i."Qty"),0) INTO batch_reserved
                        FROM public.stock_reservation_items i
                        JOIN public.stock_reservations r ON r."Id"=i."StockReservationId"
                        WHERE i."ProductBatchId"=NEW."ProductBatchId" AND i."Status"='active'
                          AND r."Status"='active' AND r."ExpiresAt">now();
                        IF batch_qty IS NULL OR batch_qty-batch_reserved<NEW."Qty" THEN
                            RAISE EXCEPTION 'STOCK_RESERVATION_INSUFFICIENT: stok batch tersedia tidak mencukupi';
                        END IF;
                    END IF;

                    IF NEW."ProductSerialId" IS NOT NULL THEN
                        UPDATE public.product_serials
                        SET "Status"='reserved',"UpdatedAt"=now()
                        WHERE "BusinessId"=NEW."BusinessId" AND "Id"=NEW."ProductSerialId"
                          AND "StockLocationId"=NEW."StockLocationId" AND "ProductId"=NEW."ProductId"
                          AND "ProductVariantId" IS NOT DISTINCT FROM NEW."ProductVariantId"
                          AND "Status" IN ('available','reserved');
                        IF NOT FOUND THEN
                            RAISE EXCEPTION 'STOCK_RESERVATION_SERIAL_UNAVAILABLE: serial tidak tersedia';
                        END IF;
                    END IF;
                    RETURN NEW;
                END
                $fn$;

                CREATE TRIGGER trg_guard_stock_reservation_item
                    BEFORE INSERT OR UPDATE ON stock_reservation_items
                    FOR EACH ROW EXECUTE FUNCTION fn_guard_stock_reservation_item();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_guard_stock_reservation_item ON stock_reservation_items;
                DROP FUNCTION IF EXISTS fn_guard_stock_reservation_item();
                """);
        }
    }
}
