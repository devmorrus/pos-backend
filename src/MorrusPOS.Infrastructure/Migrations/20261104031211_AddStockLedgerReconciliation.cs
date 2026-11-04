using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockLedgerReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_BusinessId",
                table: "stock_ledger");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedBy",
                table: "stock_ledger",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemGenerated",
                table: "stock_ledger",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PostingKey",
                table: "stock_ledger",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stock_reconciliation_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ScannedCount = table.Column<int>(type: "integer", nullable: false),
                    MismatchCount = table.Column<int>(type: "integer", nullable: false),
                    RepairableCount = table.Column<int>(type: "integer", nullable: false),
                    RepairedCount = table.Column<int>(type: "integer", nullable: false),
                    BlockedCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_reconciliation_runs", x => x.Id);
                    table.CheckConstraint("CK_stock_reconciliation_runs_Mode", "\"Mode\" IN ('preview','repair')");
                    table.CheckConstraint("CK_stock_reconciliation_runs_Status", "\"Status\" IN ('running','completed','failed','blocked')");
                    table.ForeignKey(
                        name: "FK_stock_reconciliation_runs_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reconciliation_runs_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reconciliation_runs_users_RequestedBy",
                        column: x => x.RequestedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Adopt the currently visible stock balance as the cutover opening truth.
            // The projection trigger is disabled while baseline rows are inserted so
            // existing operational balances do not change during migration.
            migrationBuilder.Sql("""
                LOCK TABLE stock_ledger IN ACCESS EXCLUSIVE MODE;
                LOCK TABLE inventory_stock IN ACCESS EXCLUSIVE MODE;

                DO $check_none$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM inventory_stock s
                        JOIN products p ON p."BusinessId"=s."BusinessId" AND p."Id"=s."ProductId"
                        WHERE p."TrackingMode"='none' AND s."QtyOnHand"<>0)
                    THEN
                        RAISE EXCEPTION 'Stock ledger cutover gagal: produk none memiliki saldo nonzero';
                    END IF;
                END
                $check_none$;

                DELETE FROM inventory_stock s
                USING products p
                WHERE p."BusinessId"=s."BusinessId" AND p."Id"=s."ProductId"
                  AND p."TrackingMode"='none' AND s."QtyOnHand"=0;

                ALTER TABLE stock_ledger DISABLE TRIGGER trg_stock_ledger_insert;

                UPDATE stock_ledger
                SET "PostingKey"='legacy/' || replace("Id"::text,'-',''),
                    "IsSystemGenerated"=false
                WHERE "PostingKey" IS NULL;

                WITH cutover AS (SELECT gen_random_uuid() AS run_id),
                ledger AS (
                    SELECT "BusinessId","OutletId","ProductId","ProductVariantId",sum("QtyChange") AS qty
                    FROM stock_ledger
                    GROUP BY "BusinessId","OutletId","ProductId","ProductVariantId"
                ),
                balances AS (
                    SELECT "BusinessId","OutletId","ProductId","ProductVariantId","QtyOnHand" AS qty
                    FROM inventory_stock
                ),
                delta AS (
                    SELECT
                        coalesce(b."BusinessId",l."BusinessId") AS business_id,
                        coalesce(b."OutletId",l."OutletId") AS outlet_id,
                        coalesce(b."ProductId",l."ProductId") AS product_id,
                        coalesce(b."ProductVariantId",l."ProductVariantId") AS variant_id,
                        coalesce(b.qty,0)-coalesce(l.qty,0) AS qty
                    FROM balances b
                    FULL JOIN ledger l
                      ON l."BusinessId"=b."BusinessId" AND l."OutletId"=b."OutletId"
                     AND l."ProductId"=b."ProductId"
                     AND l."ProductVariantId" IS NOT DISTINCT FROM b."ProductVariantId"
                )
                INSERT INTO stock_ledger
                    ("Id","BusinessId","ProductId","ProductVariantId","OutletId",
                     "ProductBatchId","ProductSerialId","DocumentItemId","PostingKey","IsSystemGenerated",
                     "MovementType","QtyChange","ReferenceType","ReferenceId","Note","CreatedBy","CreatedAt")
                SELECT
                    gen_random_uuid(),d.business_id,d.product_id,d.variant_id,d.outlet_id,
                    NULL,NULL,NULL,
                    'cutover/' || replace(d.business_id::text,'-','') || '/' || replace(d.outlet_id::text,'-','') || '/' ||
                    replace(d.product_id::text,'-','') || '/' || coalesce(replace(d.variant_id::text,'-',''),'base'),
                    true,'opening_balance',d.qty,'stock_ledger_cutover',c.run_id,
                    'Baseline Modul 1.5: adopsi saldo operasional saat cutover',NULL,now()
                FROM delta d CROSS JOIN cutover c
                JOIN products p ON p."BusinessId"=d.business_id AND p."Id"=d.product_id
                WHERE d.qty<>0 AND p."TrackingMode"<>'none';

                ALTER TABLE stock_ledger ENABLE TRIGGER trg_stock_ledger_insert;

                DO $verify$
                BEGIN
                    IF EXISTS (
                        WITH ledger AS (
                            SELECT "BusinessId","OutletId","ProductId","ProductVariantId",sum("QtyChange") qty
                            FROM stock_ledger GROUP BY "BusinessId","OutletId","ProductId","ProductVariantId"
                        )
                        SELECT 1 FROM inventory_stock s
                        JOIN products p ON p."BusinessId"=s."BusinessId" AND p."Id"=s."ProductId"
                        LEFT JOIN ledger l ON l."BusinessId"=s."BusinessId" AND l."OutletId"=s."OutletId"
                          AND l."ProductId"=s."ProductId"
                          AND l."ProductVariantId" IS NOT DISTINCT FROM s."ProductVariantId"
                        WHERE p."TrackingMode"<>'none' AND s."QtyOnHand"<>coalesce(l.qty,0))
                    THEN
                        RAISE EXCEPTION 'Stock ledger cutover gagal: ledger dan saldo masih berbeda';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM products p
                        JOIN (
                            SELECT "BusinessId","ProductId",sum("QtyChange") qty
                            FROM stock_ledger GROUP BY "BusinessId","ProductId") l
                          ON l."BusinessId"=p."BusinessId" AND l."ProductId"=p."Id"
                        WHERE p."TrackingMode"='none' AND l.qty<>0)
                    THEN
                        RAISE EXCEPTION 'Stock ledger cutover gagal: produk none memiliki saldo ledger nonzero';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM stock_ledger
                        WHERE "QtyChange"=0
                           OR "MovementType" NOT IN ('opening_balance','sale','return','purchase_in','transfer_in','transfer_out','opname_adjustment','consignment_in','consignment_return')
                           OR ("MovementType" IN ('sale','transfer_out','consignment_return') AND "QtyChange">=0)
                           OR ("MovementType" IN ('return','purchase_in','transfer_in','consignment_in') AND "QtyChange"<=0)
                           OR ("ProductSerialId" IS NOT NULL AND abs("QtyChange")<>1))
                    THEN
                        RAISE EXCEPTION 'Stock ledger cutover gagal: terdapat movement lama dengan tipe, arah, atau qty tidak valid';
                    END IF;
                END
                $verify$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "PostingKey",
                table: "stock_ledger",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "stock_reconciliation_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrackingMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LedgerQty = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    StoredQty = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Difference = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    BatchQty = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    AvailableSerialCount = table.Column<int>(type: "integer", nullable: false),
                    IssueType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_reconciliation_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_reconciliation_items_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reconciliation_items_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reconciliation_items_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reconciliation_items_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reconciliation_items_stock_reconciliation_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "stock_reconciliation_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_BusinessId_OutletId_ProductId_ProductVariantId~",
                table: "stock_ledger",
                columns: new[] { "BusinessId", "OutletId", "ProductId", "ProductVariantId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_BusinessId_PostingKey",
                table: "stock_ledger",
                columns: new[] { "BusinessId", "PostingKey" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_stock_ledger_MovementDirection",
                table: "stock_ledger",
                sql: "(\"MovementType\" IN ('sale','transfer_out','consignment_return') AND \"QtyChange\" < 0) OR (\"MovementType\" IN ('return','purchase_in','transfer_in','consignment_in') AND \"QtyChange\" > 0) OR \"MovementType\" IN ('opening_balance','opname_adjustment')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_stock_ledger_MovementType",
                table: "stock_ledger",
                sql: "\"MovementType\" IN ('opening_balance','sale','return','purchase_in','transfer_in','transfer_out','opname_adjustment','consignment_in','consignment_return')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_stock_ledger_QtyNonZero",
                table: "stock_ledger",
                sql: "\"QtyChange\" <> 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_stock_ledger_SerialQty",
                table: "stock_ledger",
                sql: "\"ProductSerialId\" IS NULL OR abs(\"QtyChange\") = 1");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reconciliation_items_BusinessId_OutletId_ProductId_Pr~",
                table: "stock_reconciliation_items",
                columns: new[] { "BusinessId", "OutletId", "ProductId", "ProductVariantId" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reconciliation_items_BusinessId_RunId_IssueType",
                table: "stock_reconciliation_items",
                columns: new[] { "BusinessId", "RunId", "IssueType" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reconciliation_items_OutletId",
                table: "stock_reconciliation_items",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reconciliation_items_ProductId",
                table: "stock_reconciliation_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reconciliation_items_ProductVariantId",
                table: "stock_reconciliation_items",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reconciliation_items_RunId",
                table: "stock_reconciliation_items",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reconciliation_runs_BusinessId_OutletId_StartedAt",
                table: "stock_reconciliation_runs",
                columns: new[] { "BusinessId", "OutletId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reconciliation_runs_OutletId",
                table: "stock_reconciliation_runs",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reconciliation_runs_RequestedBy",
                table: "stock_reconciliation_runs",
                column: "RequestedBy");

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UXT_stock_reconciliation_runs_BusinessId_Id"
                    ON stock_reconciliation_runs ("BusinessId","Id");
                CREATE UNIQUE INDEX "UXT_stock_reconciliation_items_BusinessId_Id"
                    ON stock_reconciliation_items ("BusinessId","Id");

                ALTER TABLE stock_reconciliation_runs
                    ADD CONSTRAINT "FKT_stock_reconciliation_runs_outlet"
                    FOREIGN KEY ("BusinessId","OutletId") REFERENCES outlets ("BusinessId","Id") NOT VALID,
                    ADD CONSTRAINT "FKT_stock_reconciliation_runs_user"
                    FOREIGN KEY ("BusinessId","RequestedBy") REFERENCES users ("BusinessId","Id") NOT VALID;
                ALTER TABLE stock_reconciliation_runs VALIDATE CONSTRAINT "FKT_stock_reconciliation_runs_outlet";
                ALTER TABLE stock_reconciliation_runs VALIDATE CONSTRAINT "FKT_stock_reconciliation_runs_user";

                ALTER TABLE stock_reconciliation_items
                    ADD CONSTRAINT "FKT_stock_reconciliation_items_run"
                    FOREIGN KEY ("BusinessId","RunId") REFERENCES stock_reconciliation_runs ("BusinessId","Id") NOT VALID,
                    ADD CONSTRAINT "FKT_stock_reconciliation_items_outlet"
                    FOREIGN KEY ("BusinessId","OutletId") REFERENCES outlets ("BusinessId","Id") NOT VALID,
                    ADD CONSTRAINT "FKT_stock_reconciliation_items_product"
                    FOREIGN KEY ("BusinessId","ProductId") REFERENCES products ("BusinessId","Id") NOT VALID,
                    ADD CONSTRAINT "FKT_stock_reconciliation_items_variant"
                    FOREIGN KEY ("BusinessId","ProductVariantId") REFERENCES "ProductVariants" ("BusinessId","Id") NOT VALID;
                ALTER TABLE stock_reconciliation_items VALIDATE CONSTRAINT "FKT_stock_reconciliation_items_run";
                ALTER TABLE stock_reconciliation_items VALIDATE CONSTRAINT "FKT_stock_reconciliation_items_outlet";
                ALTER TABLE stock_reconciliation_items VALIDATE CONSTRAINT "FKT_stock_reconciliation_items_product";
                ALTER TABLE stock_reconciliation_items VALIDATE CONSTRAINT "FKT_stock_reconciliation_items_variant";

                ALTER TABLE stock_reconciliation_runs ENABLE ROW LEVEL SECURITY;
                ALTER TABLE stock_reconciliation_runs FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON stock_reconciliation_runs TO morrus_app
                    USING ("BusinessId"=app_current_business_id())
                    WITH CHECK ("BusinessId"=app_current_business_id());
                CREATE TRIGGER trg_prevent_business_change BEFORE UPDATE ON stock_reconciliation_runs
                    FOR EACH ROW EXECUTE FUNCTION app_prevent_business_change();

                ALTER TABLE stock_reconciliation_items ENABLE ROW LEVEL SECURITY;
                ALTER TABLE stock_reconciliation_items FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON stock_reconciliation_items TO morrus_app
                    USING ("BusinessId"=app_current_business_id())
                    WITH CHECK ("BusinessId"=app_current_business_id());
                CREATE TRIGGER trg_prevent_business_change BEFORE UPDATE ON stock_reconciliation_items
                    FOR EACH ROW EXECUTE FUNCTION app_prevent_business_change();

                CREATE OR REPLACE FUNCTION fn_guard_inventory_stock_projection()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $fn$
                DECLARE ledger_qty numeric;
                BEGIN
                    IF current_user='morrus_migration' OR pg_trigger_depth()>1 THEN
                        RETURN coalesce(NEW,OLD);
                    END IF;
                    IF TG_OP='INSERT' AND NEW."QtyOnHand"<>0 THEN
                        RAISE EXCEPTION 'STOCK_PROJECTION_DIRECT_WRITE: saldo awal harus nol';
                    ELSIF TG_OP='UPDATE' AND NEW."QtyOnHand" IS DISTINCT FROM OLD."QtyOnHand" THEN
                        RAISE EXCEPTION 'STOCK_PROJECTION_DIRECT_WRITE: QtyOnHand hanya boleh diubah dari ledger';
                    ELSIF TG_OP='UPDATE' AND (
                        NEW."Id" IS DISTINCT FROM OLD."Id" OR
                        NEW."ProductId" IS DISTINCT FROM OLD."ProductId" OR
                        NEW."ProductVariantId" IS DISTINCT FROM OLD."ProductVariantId" OR
                        NEW."OutletId" IS DISTINCT FROM OLD."OutletId")
                    THEN
                        RAISE EXCEPTION 'STOCK_PROJECTION_DIRECT_WRITE: identity saldo stok tidak boleh diubah';
                    ELSIF TG_OP='DELETE' THEN
                        SELECT coalesce(sum("QtyChange"),0) INTO ledger_qty
                        FROM stock_ledger
                        WHERE "BusinessId"=OLD."BusinessId" AND "OutletId"=OLD."OutletId"
                          AND "ProductId"=OLD."ProductId"
                          AND "ProductVariantId" IS NOT DISTINCT FROM OLD."ProductVariantId";
                        IF OLD."QtyOnHand"<>0 OR ledger_qty<>0 THEN
                            RAISE EXCEPTION 'STOCK_PROJECTION_DIRECT_WRITE: saldo aktif tidak boleh dihapus';
                        END IF;
                    END IF;
                    RETURN coalesce(NEW,OLD);
                END
                $fn$;

                DROP TRIGGER IF EXISTS trg_guard_inventory_stock_projection ON inventory_stock;
                CREATE TRIGGER trg_guard_inventory_stock_projection
                    BEFORE INSERT OR UPDATE OR DELETE ON inventory_stock
                    FOR EACH ROW EXECUTE FUNCTION fn_guard_inventory_stock_projection();

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

                CREATE OR REPLACE FUNCTION fn_prevent_stock_ledger_mutation()
                RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    RAISE EXCEPTION 'STOCK_LEDGER_IMMUTABLE: stock ledger tidak boleh diubah atau dihapus';
                END
                $fn$;
                DROP TRIGGER IF EXISTS trg_prevent_stock_ledger_mutation ON stock_ledger;
                CREATE TRIGGER trg_prevent_stock_ledger_mutation
                    BEFORE UPDATE OR DELETE ON stock_ledger
                    FOR EACH ROW EXECUTE FUNCTION fn_prevent_stock_ledger_mutation();

                CREATE OR REPLACE FUNCTION fn_reconcile_inventory_stock(p_outlet_id uuid, p_apply boolean)
                RETURNS TABLE(
                    "ProductId" uuid,
                    "ProductVariantId" uuid,
                    "TrackingMode" text,
                    "LedgerQty" numeric,
                    "StoredQty" numeric,
                    "Difference" numeric,
                    "BatchQty" numeric,
                    "AvailableSerialCount" integer,
                    "IssueType" text,
                    "Repairable" boolean,
                    "Action" text)
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path=pg_catalog,public
                AS $fn$
                DECLARE tenant_id uuid;
                BEGIN
                    tenant_id := nullif(current_setting('app.business_id',true),'')::uuid;
                    IF tenant_id IS NULL OR NOT EXISTS (
                        SELECT 1 FROM public.outlets o WHERE o."BusinessId"=tenant_id AND o."Id"=p_outlet_id)
                    THEN
                        RAISE EXCEPTION 'STOCK_RECONCILIATION_SCOPE_INVALID: outlet tidak berada dalam business aktif';
                    END IF;
                    IF p_apply THEN
                        PERFORM pg_advisory_xact_lock(hashtextextended(tenant_id::text || ':' || p_outlet_id::text,0));
                    END IF;

                    DROP TABLE IF EXISTS pg_temp.stock_reconcile_work;
                    CREATE TEMP TABLE stock_reconcile_work ON COMMIT DROP AS
                    WITH ledger AS (
                        SELECT l."ProductId",l."ProductVariantId",sum(l."QtyChange") AS qty
                        FROM public.stock_ledger l
                        WHERE l."BusinessId"=tenant_id AND l."OutletId"=p_outlet_id
                        GROUP BY l."ProductId",l."ProductVariantId"
                    ),
                    balances AS (
                        SELECT s."ProductId",s."ProductVariantId",s."QtyOnHand" AS qty,true AS balance_exists
                        FROM public.inventory_stock s
                        WHERE s."BusinessId"=tenant_id AND s."OutletId"=p_outlet_id
                    ),
                    keys AS (
                        SELECT coalesce(l."ProductId",b."ProductId") AS product_id,
                               coalesce(l."ProductVariantId",b."ProductVariantId") AS variant_id,
                               coalesce(l.qty,0) AS ledger_qty,coalesce(b.qty,0) AS stored_qty,
                               coalesce(b.balance_exists,false) AS balance_exists
                        FROM ledger l FULL JOIN balances b
                          ON l."ProductId"=b."ProductId"
                         AND l."ProductVariantId" IS NOT DISTINCT FROM b."ProductVariantId"
                    ),
                    base AS (
                        SELECT k.*,p."TrackingMode" AS tracking_mode,
                               coalesce(bt.qty,0) AS batch_qty,
                               coalesce(st.qty,0)::int AS serial_qty
                        FROM keys k
                        JOIN public.products p ON p."BusinessId"=tenant_id AND p."Id"=k.product_id
                        LEFT JOIN LATERAL (
                            SELECT sum(pb."QtyRemaining") AS qty FROM public."ProductBatches" pb
                            WHERE pb."BusinessId"=tenant_id AND pb."OutletId"=p_outlet_id
                              AND pb."ProductId"=k.product_id
                              AND pb."ProductVariantId" IS NOT DISTINCT FROM k.variant_id) bt ON true
                        LEFT JOIN LATERAL (
                            SELECT count(*) AS qty FROM public.product_serials ps
                            WHERE ps."BusinessId"=tenant_id AND ps."OutletId"=p_outlet_id
                              AND ps."ProductId"=k.product_id
                              AND ps."ProductVariantId" IS NOT DISTINCT FROM k.variant_id
                              AND ps."Status"='available') st ON true
                    ),
                    classified AS (
                        SELECT b.*,
                            CASE
                              WHEN ledger_qty<0 THEN 'negative_ledger'
                              WHEN tracking_mode='none' AND (ledger_qty<>0 OR stored_qty<>0) THEN 'untracked_product_nonzero'
                              WHEN tracking_mode='batch' AND batch_qty<>ledger_qty THEN 'batch_projection_mismatch'
                              WHEN tracking_mode='serial' AND serial_qty<>ledger_qty THEN 'serial_projection_mismatch'
                              WHEN ledger_qty<>stored_qty THEN 'balance_mismatch'
                              ELSE NULL
                            END AS issue_type
                        FROM base b)
                    SELECT product_id AS "ProductId",variant_id AS "ProductVariantId",
                           tracking_mode::text AS "TrackingMode",ledger_qty AS "LedgerQty",
                           stored_qty AS "StoredQty",ledger_qty-stored_qty AS "Difference",
                           batch_qty AS "BatchQty",serial_qty AS "AvailableSerialCount",
                           issue_type::text AS "IssueType",issue_type='balance_mismatch' AS "Repairable",
                           balance_exists AS "BalanceExists"
                    FROM classified WHERE issue_type IS NOT NULL;

                    IF p_apply AND EXISTS (SELECT 1 FROM stock_reconcile_work w WHERE NOT w."Repairable") THEN
                        RAISE EXCEPTION 'STOCK_RECONCILIATION_BLOCKED: terdapat proyeksi yang tidak aman diperbaiki otomatis';
                    END IF;
                    IF p_apply THEN
                        UPDATE public.inventory_stock s
                        SET "QtyOnHand"=w."LedgerQty","UpdatedAt"=now()
                        FROM stock_reconcile_work w
                        WHERE w."Repairable" AND w."BalanceExists"
                          AND s."BusinessId"=tenant_id AND s."OutletId"=p_outlet_id
                          AND s."ProductId"=w."ProductId"
                          AND s."ProductVariantId" IS NOT DISTINCT FROM w."ProductVariantId";
                        INSERT INTO public.inventory_stock
                            ("Id","BusinessId","ProductId","ProductVariantId","OutletId","QtyOnHand","MinStockAlert","UpdatedAt")
                        SELECT gen_random_uuid(),tenant_id,w."ProductId",w."ProductVariantId",p_outlet_id,w."LedgerQty",0,now()
                        FROM stock_reconcile_work w
                        WHERE w."Repairable" AND NOT w."BalanceExists" AND w."LedgerQty"<>0;
                    END IF;

                    RETURN QUERY SELECT w."ProductId",w."ProductVariantId",w."TrackingMode",
                        w."LedgerQty",w."StoredQty",w."Difference",w."BatchQty",w."AvailableSerialCount",
                        w."IssueType",w."Repairable",
                        CASE WHEN NOT p_apply THEN 'none'
                             WHEN NOT w."Repairable" THEN 'blocked'
                             WHEN w."BalanceExists" THEN 'updated_balance'
                             ELSE 'inserted_balance' END::text
                    FROM stock_reconcile_work w;
                END
                $fn$;

                REVOKE ALL ON FUNCTION fn_update_inventory_stock() FROM PUBLIC;
                REVOKE ALL ON FUNCTION fn_reconcile_inventory_stock(uuid,boolean) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION fn_update_inventory_stock() TO morrus_app;
                GRANT EXECUTE ON FUNCTION fn_reconcile_inventory_stock(uuid,boolean) TO morrus_app;
                REVOKE UPDATE,DELETE ON stock_ledger FROM morrus_app;
                GRANT SELECT,INSERT ON stock_ledger TO morrus_app;
                REVOKE ALL ON stock_reconciliation_runs,stock_reconciliation_items FROM morrus_app;
                GRANT SELECT,INSERT ON stock_reconciliation_runs,stock_reconciliation_items TO morrus_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS fn_reconcile_inventory_stock(uuid,boolean);
                DROP TRIGGER IF EXISTS trg_prevent_stock_ledger_mutation ON stock_ledger;
                DROP FUNCTION IF EXISTS fn_prevent_stock_ledger_mutation();
                DROP TRIGGER IF EXISTS trg_guard_inventory_stock_projection ON inventory_stock;
                DROP FUNCTION IF EXISTS fn_guard_inventory_stock_projection();
                DROP TRIGGER IF EXISTS trg_prevent_business_change ON stock_reconciliation_items;
                DROP TRIGGER IF EXISTS trg_prevent_business_change ON stock_reconciliation_runs;
                DROP POLICY IF EXISTS tenant_isolation ON stock_reconciliation_items;
                DROP POLICY IF EXISTS tenant_isolation ON stock_reconciliation_runs;
                ALTER TABLE stock_ledger DISABLE TRIGGER trg_stock_ledger_insert;
                DELETE FROM stock_ledger WHERE "ReferenceType"='stock_ledger_cutover' AND "IsSystemGenerated"=true;
                ALTER TABLE stock_ledger ENABLE TRIGGER trg_stock_ledger_insert;
                GRANT UPDATE,DELETE ON stock_ledger TO morrus_app;

                CREATE OR REPLACE FUNCTION fn_update_inventory_stock()
                RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    IF NEW."ProductVariantId" IS NOT NULL THEN
                        INSERT INTO inventory_stock
                            ("Id","BusinessId","ProductId","ProductVariantId","OutletId","QtyOnHand","MinStockAlert","UpdatedAt")
                        VALUES (gen_random_uuid(),NEW."BusinessId",NEW."ProductId",NEW."ProductVariantId",NEW."OutletId",NEW."QtyChange",0,now())
                        ON CONFLICT ("BusinessId","ProductId","ProductVariantId","OutletId") WHERE "ProductVariantId" IS NOT NULL
                        DO UPDATE SET "QtyOnHand"=inventory_stock."QtyOnHand"+NEW."QtyChange","UpdatedAt"=now();
                    ELSE
                        INSERT INTO inventory_stock
                            ("Id","BusinessId","ProductId","OutletId","QtyOnHand","MinStockAlert","UpdatedAt")
                        VALUES (gen_random_uuid(),NEW."BusinessId",NEW."ProductId",NEW."OutletId",NEW."QtyChange",0,now())
                        ON CONFLICT ("BusinessId","ProductId","OutletId") WHERE "ProductVariantId" IS NULL
                        DO UPDATE SET "QtyOnHand"=inventory_stock."QtyOnHand"+NEW."QtyChange","UpdatedAt"=now();
                    END IF;
                    RETURN NEW;
                END
                $fn$;
                """);

            migrationBuilder.DropTable(
                name: "stock_reconciliation_items");

            migrationBuilder.DropTable(
                name: "stock_reconciliation_runs");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_BusinessId_OutletId_ProductId_ProductVariantId~",
                table: "stock_ledger");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_BusinessId_PostingKey",
                table: "stock_ledger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_stock_ledger_MovementDirection",
                table: "stock_ledger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_stock_ledger_MovementType",
                table: "stock_ledger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_stock_ledger_QtyNonZero",
                table: "stock_ledger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_stock_ledger_SerialQty",
                table: "stock_ledger");

            migrationBuilder.DropColumn(
                name: "IsSystemGenerated",
                table: "stock_ledger");

            migrationBuilder.DropColumn(
                name: "PostingKey",
                table: "stock_ledger");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedBy",
                table: "stock_ledger",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_BusinessId",
                table: "stock_ledger",
                column: "BusinessId");
        }
    }
}
