using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockReservationsAndAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_product_serials_Status",
                table: "product_serials");

            migrationBuilder.DropCheckConstraint(
                name: "CK_idempotency_records_Operation",
                table: "idempotency_records");

            migrationBuilder.CreateTable(
                name: "stock_reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegisterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsumedByTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReleaseReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_reservations", x => x.Id);
                    table.CheckConstraint("CK_stock_reservations_Lifecycle", "(\"Status\" = 'active' AND \"ResolvedAt\" IS NULL AND \"ConsumedByTransactionId\" IS NULL) OR (\"Status\" = 'consumed' AND \"ResolvedAt\" IS NOT NULL AND \"ConsumedByTransactionId\" IS NOT NULL) OR (\"Status\" IN ('released','expired') AND \"ResolvedAt\" IS NOT NULL AND \"ConsumedByTransactionId\" IS NULL)");
                    table.CheckConstraint("CK_stock_reservations_Source", "\"SourceType\" IN ('pos','channel_order')");
                    table.CheckConstraint("CK_stock_reservations_Status", "\"Status\" IN ('active','consumed','released','expired')");
                    table.ForeignKey(
                        name: "FK_stock_reservations_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservations_cashier_sessions_CashierSessionId",
                        column: x => x.CashierSessionId,
                        principalTable: "cashier_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservations_pos_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "pos_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservations_pos_registers_RegisterId",
                        column: x => x.RegisterId,
                        principalTable: "pos_registers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservations_stock_locations_StockLocationId",
                        column: x => x.StockLocationId,
                        principalTable: "stock_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservations_transactions_ConsumedByTransactionId",
                        column: x => x.ConsumedByTransactionId,
                        principalTable: "transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservations_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_reservation_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductSerialId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrackingModeSnapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_reservation_items", x => x.Id);
                    table.CheckConstraint("CK_stock_reservation_items_Qty", "\"Qty\" > 0");
                    table.CheckConstraint("CK_stock_reservation_items_Status", "\"Status\" IN ('active','consumed','released','expired')");
                    table.CheckConstraint("CK_stock_reservation_items_TrackingMode", "\"TrackingModeSnapshot\" IN ('quantity','batch','serial')");
                    table.CheckConstraint("CK_stock_reservation_items_TrackingReference", "(\"TrackingModeSnapshot\" = 'quantity' AND \"ProductBatchId\" IS NULL AND \"ProductSerialId\" IS NULL) OR (\"TrackingModeSnapshot\" = 'batch' AND \"ProductBatchId\" IS NOT NULL AND \"ProductSerialId\" IS NULL) OR (\"TrackingModeSnapshot\" = 'serial' AND \"ProductBatchId\" IS NULL AND \"ProductSerialId\" IS NOT NULL AND \"Qty\" = 1)");
                    table.ForeignKey(
                        name: "FK_stock_reservation_items_ProductBatches_ProductBatchId",
                        column: x => x.ProductBatchId,
                        principalTable: "ProductBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservation_items_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservation_items_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservation_items_product_serials_ProductSerialId",
                        column: x => x.ProductSerialId,
                        principalTable: "product_serials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservation_items_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservation_items_stock_locations_StockLocationId",
                        column: x => x.StockLocationId,
                        principalTable: "stock_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservation_items_stock_reservations_StockReservation~",
                        column: x => x.StockReservationId,
                        principalTable: "stock_reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_product_serials_Status",
                table: "product_serials",
                sql: "\"Status\" IN ('available','reserved','sold','in_transfer','returned_to_supplier','written_off')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_idempotency_records_Operation",
                table: "idempotency_records",
                sql: "\"Operation\" IN ('checkout','refund','supplier_payment','stock_transfer_send','stock_transfer_receive','stock_reservation_create')");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservation_items_BusinessId_ProductBatchId_Status",
                table: "stock_reservation_items",
                columns: new[] { "BusinessId", "ProductBatchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservation_items_BusinessId_StockLocationId_ProductI~",
                table: "stock_reservation_items",
                columns: new[] { "BusinessId", "StockLocationId", "ProductId", "ProductVariantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservation_items_ProductBatchId",
                table: "stock_reservation_items",
                column: "ProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservation_items_ProductId",
                table: "stock_reservation_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservation_items_ProductSerialId",
                table: "stock_reservation_items",
                column: "ProductSerialId",
                unique: true,
                filter: "\"ProductSerialId\" IS NOT NULL AND \"Status\" = 'active'");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservation_items_ProductVariantId",
                table: "stock_reservation_items",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservation_items_StockLocationId",
                table: "stock_reservation_items",
                column: "StockLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservation_items_StockReservationId",
                table: "stock_reservation_items",
                column: "StockReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_BusinessId_CashierSessionId_Status",
                table: "stock_reservations",
                columns: new[] { "BusinessId", "CashierSessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_BusinessId_Status_ExpiresAt",
                table: "stock_reservations",
                columns: new[] { "BusinessId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_BusinessId_StockLocationId_Status_Expire~",
                table: "stock_reservations",
                columns: new[] { "BusinessId", "StockLocationId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_CashierSessionId",
                table: "stock_reservations",
                column: "CashierSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_ConsumedByTransactionId",
                table: "stock_reservations",
                column: "ConsumedByTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_CreatedBy",
                table: "stock_reservations",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_DeviceId",
                table: "stock_reservations",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_RegisterId",
                table: "stock_reservations",
                column: "RegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_StockLocationId",
                table: "stock_reservations",
                column: "StockLocationId");

            migrationBuilder.Sql("""
                ALTER TABLE stock_reservations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE stock_reservations FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON stock_reservations TO morrus_app
                    USING ("BusinessId"=app_current_business_id())
                    WITH CHECK ("BusinessId"=app_current_business_id());
                CREATE TRIGGER trg_prevent_business_change BEFORE UPDATE ON stock_reservations
                    FOR EACH ROW EXECUTE FUNCTION app_prevent_business_change();

                ALTER TABLE stock_reservation_items ENABLE ROW LEVEL SECURITY;
                ALTER TABLE stock_reservation_items FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON stock_reservation_items TO morrus_app
                    USING ("BusinessId"=app_current_business_id())
                    WITH CHECK ("BusinessId"=app_current_business_id());
                CREATE TRIGGER trg_prevent_business_change BEFORE UPDATE ON stock_reservation_items
                    FOR EACH ROW EXECUTE FUNCTION app_prevent_business_change();

                GRANT SELECT, INSERT, UPDATE, DELETE ON stock_reservations, stock_reservation_items TO morrus_app;
                GRANT SELECT, UPDATE ON stock_reservations, stock_reservation_items, product_serials TO morrus_system;
                GRANT INSERT ON outbox_messages TO morrus_system;

                CREATE INDEX "IX_stock_ledger_ReservationSales"
                    ON stock_ledger ("BusinessId","StockLocationId","ProductId","ProductVariantId","MovementType")
                    WHERE "MovementType" IN ('sale','return');

                CREATE OR REPLACE FUNCTION fn_active_reserved_qty(
                    p_business_id uuid,
                    p_stock_location_id uuid,
                    p_product_id uuid,
                    p_product_variant_id uuid)
                RETURNS numeric
                LANGUAGE sql
                SECURITY DEFINER
                STABLE
                SET search_path=pg_catalog,public
                AS $fn$
                    SELECT coalesce(sum(i."Qty"),0)
                    FROM public.stock_reservation_items i
                    JOIN public.stock_reservations r ON r."Id"=i."StockReservationId"
                    WHERE i."BusinessId"=p_business_id
                      AND i."StockLocationId"=p_stock_location_id
                      AND i."ProductId"=p_product_id
                      AND i."ProductVariantId" IS NOT DISTINCT FROM p_product_variant_id
                      AND i."Status"='active'
                      AND r."Status"='active'
                      AND r."ExpiresAt">now();
                $fn$;

                CREATE OR REPLACE FUNCTION fn_update_inventory_stock()
                RETURNS trigger
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path=pg_catalog,public
                AS $fn$
                DECLARE
                    reserved_qty numeric;
                    current_qty numeric;
                BEGIN
                    PERFORM pg_advisory_xact_lock_shared(hashtextextended(NEW."BusinessId"::text || ':' || NEW."StockLocationId"::text,0));
                    IF NOT EXISTS (
                        SELECT 1 FROM public.products p
                        WHERE p."BusinessId"=NEW."BusinessId" AND p."Id"=NEW."ProductId"
                          AND p."TrackingMode"<>'none')
                    THEN
                        RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: produk tidak valid atau tidak memiliki stok';
                    END IF;
                    IF NOT EXISTS (
                        SELECT 1 FROM public.stock_locations l
                        WHERE l."BusinessId"=NEW."BusinessId" AND l."Id"=NEW."StockLocationId" AND l."IsActive")
                    THEN
                        RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: lokasi stok tidak valid';
                    END IF;

                    reserved_qty := public.fn_active_reserved_qty(
                        NEW."BusinessId",NEW."StockLocationId",NEW."ProductId",NEW."ProductVariantId");

                    IF NEW."ProductVariantId" IS NOT NULL THEN
                        UPDATE public.inventory_stock
                        SET "QtyOnHand"="QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now()
                        WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId"
                          AND "ProductVariantId"=NEW."ProductVariantId" AND "StockLocationId"=NEW."StockLocationId"
                          AND "QtyOnHand"+NEW."QtyChange">=reserved_qty;
                        IF FOUND THEN RETURN NEW; END IF;
                        SELECT "QtyOnHand" INTO current_qty FROM public.inventory_stock
                        WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId"
                          AND "ProductVariantId"=NEW."ProductVariantId" AND "StockLocationId"=NEW."StockLocationId";
                    ELSE
                        UPDATE public.inventory_stock
                        SET "QtyOnHand"="QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now()
                        WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId"
                          AND "ProductVariantId" IS NULL AND "StockLocationId"=NEW."StockLocationId"
                          AND "QtyOnHand"+NEW."QtyChange">=reserved_qty;
                        IF FOUND THEN RETURN NEW; END IF;
                        SELECT "QtyOnHand" INTO current_qty FROM public.inventory_stock
                        WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId"
                          AND "ProductVariantId" IS NULL AND "StockLocationId"=NEW."StockLocationId";
                    END IF;

                    IF current_qty IS NOT NULL THEN
                        IF current_qty+NEW."QtyChange">=0 AND current_qty+NEW."QtyChange"<reserved_qty THEN
                            RAISE EXCEPTION 'STOCK_RESERVED_STOCK_CONFLICT: stok sedang dipesan dan tidak tersedia untuk pergerakan ini';
                        END IF;
                        RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: stok tidak mencukupi';
                    END IF;
                    IF NEW."QtyChange"<0 THEN
                        RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: stok tidak mencukupi';
                    END IF;

                    IF NEW."ProductVariantId" IS NOT NULL THEN
                        INSERT INTO public.inventory_stock
                            ("Id","BusinessId","ProductId","ProductVariantId","StockLocationId","QtyOnHand","MinStockAlert","UpdatedAt")
                        VALUES
                            (gen_random_uuid(),NEW."BusinessId",NEW."ProductId",NEW."ProductVariantId",NEW."StockLocationId",NEW."QtyChange",0,now())
                        ON CONFLICT ("BusinessId","ProductId","ProductVariantId","StockLocationId")
                            WHERE "ProductVariantId" IS NOT NULL
                        DO UPDATE SET "QtyOnHand"=public.inventory_stock."QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now();
                    ELSE
                        INSERT INTO public.inventory_stock
                            ("Id","BusinessId","ProductId","StockLocationId","QtyOnHand","MinStockAlert","UpdatedAt")
                        VALUES
                            (gen_random_uuid(),NEW."BusinessId",NEW."ProductId",NEW."StockLocationId",NEW."QtyChange",0,now())
                        ON CONFLICT ("BusinessId","ProductId","StockLocationId") WHERE "ProductVariantId" IS NULL
                        DO UPDATE SET "QtyOnHand"=public.inventory_stock."QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now();
                    END IF;
                    RETURN NEW;
                END
                $fn$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $check$
                BEGIN
                    IF EXISTS (SELECT 1 FROM stock_reservations WHERE "Status"='active' AND "ExpiresAt">now()) THEN
                        RAISE EXCEPTION 'STOCK_RESERVATION_ACTIVE: release reservasi aktif sebelum rollback';
                    END IF;
                END $check$;
                UPDATE product_serials SET "Status"='available', "UpdatedAt"=now() WHERE "Status"='reserved';
                DELETE FROM idempotency_records WHERE "Operation"='stock_reservation_create';
                DROP INDEX IF EXISTS "IX_stock_ledger_ReservationSales";
                DROP FUNCTION IF EXISTS fn_active_reserved_qty(uuid,uuid,uuid,uuid);
                """);

            migrationBuilder.DropTable(
                name: "stock_reservation_items");

            migrationBuilder.DropTable(
                name: "stock_reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_product_serials_Status",
                table: "product_serials");

            migrationBuilder.DropCheckConstraint(
                name: "CK_idempotency_records_Operation",
                table: "idempotency_records");

            migrationBuilder.AddCheckConstraint(
                name: "CK_product_serials_Status",
                table: "product_serials",
                sql: "\"Status\" IN ('available','sold','in_transfer','returned_to_supplier','written_off')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_idempotency_records_Operation",
                table: "idempotency_records",
                sql: "\"Operation\" IN ('checkout','refund','supplier_payment','stock_transfer_send','stock_transfer_receive')");

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_update_inventory_stock()
                RETURNS trigger
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path=pg_catalog,public
                AS $fn$
                BEGIN
                    PERFORM pg_advisory_xact_lock_shared(hashtextextended(NEW."BusinessId"::text || ':' || NEW."StockLocationId"::text,0));
                    IF NOT EXISTS (SELECT 1 FROM public.products p WHERE p."BusinessId"=NEW."BusinessId" AND p."Id"=NEW."ProductId" AND p."TrackingMode"<>'none') THEN
                        RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: produk tidak valid atau tidak memiliki stok';
                    END IF;
                    IF NEW."ProductVariantId" IS NOT NULL THEN
                        UPDATE public.inventory_stock SET "QtyOnHand"="QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now()
                        WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId" AND "ProductVariantId"=NEW."ProductVariantId"
                          AND "StockLocationId"=NEW."StockLocationId" AND "QtyOnHand"+NEW."QtyChange">=0;
                    ELSE
                        UPDATE public.inventory_stock SET "QtyOnHand"="QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now()
                        WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId" AND "ProductVariantId" IS NULL
                          AND "StockLocationId"=NEW."StockLocationId" AND "QtyOnHand"+NEW."QtyChange">=0;
                    END IF;
                    IF FOUND THEN RETURN NEW; END IF;
                    IF NEW."QtyChange"<0 THEN RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: stok tidak mencukupi'; END IF;
                    IF NEW."ProductVariantId" IS NOT NULL THEN
                        INSERT INTO public.inventory_stock ("Id","BusinessId","ProductId","ProductVariantId","StockLocationId","QtyOnHand","MinStockAlert","UpdatedAt")
                        VALUES (gen_random_uuid(),NEW."BusinessId",NEW."ProductId",NEW."ProductVariantId",NEW."StockLocationId",NEW."QtyChange",0,now())
                        ON CONFLICT ("BusinessId","ProductId","ProductVariantId","StockLocationId") WHERE "ProductVariantId" IS NOT NULL
                        DO UPDATE SET "QtyOnHand"=public.inventory_stock."QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now();
                    ELSE
                        INSERT INTO public.inventory_stock ("Id","BusinessId","ProductId","StockLocationId","QtyOnHand","MinStockAlert","UpdatedAt")
                        VALUES (gen_random_uuid(),NEW."BusinessId",NEW."ProductId",NEW."StockLocationId",NEW."QtyChange",0,now())
                        ON CONFLICT ("BusinessId","ProductId","StockLocationId") WHERE "ProductVariantId" IS NULL
                        DO UPDATE SET "QtyOnHand"=public.inventory_stock."QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now();
                    END IF;
                    RETURN NEW;
                END
                $fn$;
                """);
        }
    }
}
