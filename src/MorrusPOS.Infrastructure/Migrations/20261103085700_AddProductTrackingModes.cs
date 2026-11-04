using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductTrackingModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_BusinessId",
                table: "ProductBatches");

            migrationBuilder.AddColumn<string>(
                name: "TrackingModeSnapshot",
                table: "transaction_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "quantity");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductBatchId",
                table: "stock_opname_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductSerialId",
                table: "stock_opname_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentItemId",
                table: "stock_ledger",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductBatchId",
                table: "stock_ledger",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductSerialId",
                table: "stock_ledger",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingMode",
                table: "products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "quantity");

            migrationBuilder.AlterColumn<decimal>(
                name: "QtyRemaining",
                table: "ProductBatches",
                type: "numeric(12,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "QtyProduction",
                table: "ProductBatches",
                type: "numeric(12,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpiryDate",
                table: "ProductBatches",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "BatchNumber",
                table: "ProductBatches",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "OutletId",
                table: "ProductBatches",
                type: "uuid",
                nullable: true);

            // Legacy batches were not outlet-aware. Resolve the outlet through the
            // receiving note that created them, then fall back to the only outlet
            // of the business. Anything ambiguous must stop the migration instead
            // of silently assigning stock to the wrong outlet.
            migrationBuilder.Sql(@"
                UPDATE ""ProductBatches"" b
                SET ""OutletId"" = resolved.""OutletId""
                FROM (
                    SELECT pb.""Id"", MIN(po.""OutletId""::text)::uuid AS ""OutletId""
                    FROM ""ProductBatches"" pb
                    JOIN ""ReceivingNoteItems"" rni
                      ON rni.""BusinessId"" = pb.""BusinessId""
                     AND rni.""ProductId"" = pb.""ProductId""
                     AND rni.""ProductVariantId"" IS NOT DISTINCT FROM pb.""ProductVariantId""
                     AND rni.""BatchNumber"" = pb.""BatchNumber""
                    JOIN ""ReceivingNotes"" rn ON rn.""Id"" = rni.""ReceivingNoteId""
                    JOIN purchase_orders po ON po.""Id"" = rn.""PurchaseOrderId""
                    GROUP BY pb.""Id""
                    HAVING COUNT(DISTINCT po.""OutletId"") = 1
                ) resolved
                WHERE b.""Id"" = resolved.""Id"";

                UPDATE ""ProductBatches"" b
                SET ""OutletId"" = o.""Id""
                FROM ""outlets"" o
                WHERE b.""OutletId"" IS NULL
                  AND o.""BusinessId"" = b.""BusinessId""
                  AND (SELECT COUNT(*) FROM ""outlets"" o2 WHERE o2.""BusinessId"" = b.""BusinessId"") = 1;

                DO $backfill$
                DECLARE unresolved_count integer;
                BEGIN
                    SELECT COUNT(*) INTO unresolved_count
                    FROM ""ProductBatches"" WHERE ""OutletId"" IS NULL;

                    IF unresolved_count > 0 THEN
                        RAISE EXCEPTION
                            'Migrasi AddProductTrackingModes gagal: % batch lama tidak dapat dipetakan ke outlet. Lengkapi data ReceivingNote/PurchaseOrder atau tetapkan outlet secara manual sebelum migrasi.',
                            unresolved_count;
                    END IF;
                END
                $backfill$;

                DO $duplicates$
                DECLARE duplicate_batches text;
                BEGIN
                    SELECT string_agg(format('%s/%s', d.""ProductId"", d.""BatchNumber""), ', ')
                    INTO duplicate_batches
                    FROM (
                        SELECT ""ProductId"", ""BatchNumber""
                        FROM ""ProductBatches""
                        GROUP BY ""BusinessId"", ""OutletId"", ""ProductId"", ""ProductVariantId"", ""BatchNumber""
                        HAVING COUNT(*) > 1
                    ) d;

                    IF duplicate_batches IS NOT NULL THEN
                        RAISE EXCEPTION
                            'Migrasi AddProductTrackingModes gagal: batch number duplikat pada outlet yang sama (%). Gabungkan atau ganti nomor batch sebelum migrasi.',
                            duplicate_batches;
                    END IF;
                END
                $duplicates$;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "OutletId",
                table: "ProductBatches",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "product_serials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivingNoteItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    NormalizedSerialNumber = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SoldAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_serials", x => x.Id);
                    table.CheckConstraint("CK_product_serials_Status", "\"Status\" IN ('available','sold','in_transfer','returned_to_supplier','written_off')");
                    table.ForeignKey(
                        name: "FK_product_serials_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_serials_ReceivingNoteItems_ReceivingNoteItemId",
                        column: x => x.ReceivingNoteItemId,
                        principalTable: "ReceivingNoteItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_product_serials_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_serials_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_serials_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "capabilities",
                columns: new[] { "Id", "Code", "CreatedAt", "DefaultEnabled", "Description", "IsActive", "IsTenantConfigurable", "Module", "Name", "Scope", "SortOrder", "UpdatedAt" },
                values: new object[] { new Guid("ca200000-0000-4000-8000-000000000028"), "catalog.serial_tracking", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Serial / IMEI Produk", true, true, "Catalog", "Serial / IMEI Produk", "Business", 28, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "industry_profile_capabilities",
                columns: new[] { "CapabilityId", "IndustryProfileId", "IsEnabled" },
                values: new object[,]
                {
                    { new Guid("ca200000-0000-4000-8000-000000000028"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000028"), new Guid("ca100002-0000-4000-8000-000000000002"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000028"), new Guid("ca100003-0000-4000-8000-000000000003"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000028"), new Guid("ca100004-0000-4000-8000-000000000004"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000028"), new Guid("ca100005-0000-4000-8000-000000000005"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000028"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000028"), new Guid("ca100007-0000-4000-8000-000000000007"), true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_opname_items_ProductBatchId",
                table: "stock_opname_items",
                column: "ProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_opname_items_ProductSerialId",
                table: "stock_opname_items",
                column: "ProductSerialId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_ProductBatchId",
                table: "stock_ledger",
                column: "ProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_ProductSerialId",
                table: "stock_ledger",
                column: "ProductSerialId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_stock_ledger_TrackingReference",
                table: "stock_ledger",
                sql: "NOT (\"ProductBatchId\" IS NOT NULL AND \"ProductSerialId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_stock_NonNegative",
                table: "inventory_stock",
                sql: "\"QtyOnHand\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_TrackingMode",
                table: "products",
                sql: "\"TrackingMode\" IN ('none','quantity','batch','serial')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_transaction_items_TrackingModeSnapshot",
                table: "transaction_items",
                sql: "\"TrackingModeSnapshot\" IN ('none','quantity','batch','serial')");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_BusinessId_OutletId_ProductId_BatchNumber",
                table: "ProductBatches",
                columns: new[] { "BusinessId", "OutletId", "ProductId", "BatchNumber" },
                unique: true,
                filter: "\"ProductVariantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_BusinessId_OutletId_ProductId_ProductVariant~",
                table: "ProductBatches",
                columns: new[] { "BusinessId", "OutletId", "ProductId", "ProductVariantId", "BatchNumber" },
                unique: true,
                filter: "\"ProductVariantId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_OutletId_ProductId_ExpiryDate",
                table: "ProductBatches",
                columns: new[] { "OutletId", "ProductId", "ExpiryDate" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductBatches_Qty",
                table: "ProductBatches",
                sql: "\"QtyProduction\" >= 0 AND \"QtyRemaining\" >= 0 AND \"QtyRemaining\" <= \"QtyProduction\"");

            migrationBuilder.CreateIndex(
                name: "IX_product_serials_BusinessId_NormalizedSerialNumber",
                table: "product_serials",
                columns: new[] { "BusinessId", "NormalizedSerialNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_serials_BusinessId_OutletId_ProductId_ProductVarian~",
                table: "product_serials",
                columns: new[] { "BusinessId", "OutletId", "ProductId", "ProductVariantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_product_serials_OutletId",
                table: "product_serials",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_product_serials_ProductId",
                table: "product_serials",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_serials_ProductVariantId",
                table: "product_serials",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_product_serials_ReceivingNoteItemId",
                table: "product_serials",
                column: "ReceivingNoteItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_outlets_OutletId",
                table: "ProductBatches",
                column: "OutletId",
                principalTable: "outlets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_ledger_ProductBatches_ProductBatchId",
                table: "stock_ledger",
                column: "ProductBatchId",
                principalTable: "ProductBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_ledger_product_serials_ProductSerialId",
                table: "stock_ledger",
                column: "ProductSerialId",
                principalTable: "product_serials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_opname_items_ProductBatches_ProductBatchId",
                table: "stock_opname_items",
                column: "ProductBatchId",
                principalTable: "ProductBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_opname_items_product_serials_ProductSerialId",
                table: "stock_opname_items",
                column: "ProductSerialId",
                principalTable: "product_serials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""UXT_product_serials_BusinessId_Id""
                    ON product_serials (""BusinessId"",""Id"");

                ALTER TABLE product_serials
                    ADD CONSTRAINT ""FKT_product_serials_product""
                    FOREIGN KEY (""BusinessId"",""ProductId"") REFERENCES products (""BusinessId"",""Id"") NOT VALID;
                ALTER TABLE product_serials VALIDATE CONSTRAINT ""FKT_product_serials_product"";
                ALTER TABLE product_serials
                    ADD CONSTRAINT ""FKT_product_serials_variant""
                    FOREIGN KEY (""BusinessId"",""ProductVariantId"") REFERENCES ""ProductVariants"" (""BusinessId"",""Id"") NOT VALID;
                ALTER TABLE product_serials VALIDATE CONSTRAINT ""FKT_product_serials_variant"";
                ALTER TABLE product_serials
                    ADD CONSTRAINT ""FKT_product_serials_outlet""
                    FOREIGN KEY (""BusinessId"",""OutletId"") REFERENCES outlets (""BusinessId"",""Id"") NOT VALID;
                ALTER TABLE product_serials VALIDATE CONSTRAINT ""FKT_product_serials_outlet"";
                ALTER TABLE product_serials
                    ADD CONSTRAINT ""FKT_product_serials_receiving_item""
                    FOREIGN KEY (""BusinessId"",""ReceivingNoteItemId"") REFERENCES ""ReceivingNoteItems"" (""BusinessId"",""Id"") NOT VALID;
                ALTER TABLE product_serials VALIDATE CONSTRAINT ""FKT_product_serials_receiving_item"";

                ALTER TABLE ""ProductBatches""
                    ADD CONSTRAINT ""FKT_ProductBatches_outlet""
                    FOREIGN KEY (""BusinessId"",""OutletId"") REFERENCES outlets (""BusinessId"",""Id"") NOT VALID;
                ALTER TABLE ""ProductBatches"" VALIDATE CONSTRAINT ""FKT_ProductBatches_outlet"";

                ALTER TABLE stock_ledger
                    ADD CONSTRAINT ""FKT_stock_ledger_batch""
                    FOREIGN KEY (""BusinessId"",""ProductBatchId"") REFERENCES ""ProductBatches"" (""BusinessId"",""Id"") NOT VALID;
                ALTER TABLE stock_ledger VALIDATE CONSTRAINT ""FKT_stock_ledger_batch"";
                ALTER TABLE stock_ledger
                    ADD CONSTRAINT ""FKT_stock_ledger_serial""
                    FOREIGN KEY (""BusinessId"",""ProductSerialId"") REFERENCES product_serials (""BusinessId"",""Id"") NOT VALID;
                ALTER TABLE stock_ledger VALIDATE CONSTRAINT ""FKT_stock_ledger_serial"";

                ALTER TABLE stock_opname_items
                    ADD CONSTRAINT ""FKT_stock_opname_items_batch""
                    FOREIGN KEY (""BusinessId"",""ProductBatchId"") REFERENCES ""ProductBatches"" (""BusinessId"",""Id"") NOT VALID;
                ALTER TABLE stock_opname_items VALIDATE CONSTRAINT ""FKT_stock_opname_items_batch"";
                ALTER TABLE stock_opname_items
                    ADD CONSTRAINT ""FKT_stock_opname_items_serial""
                    FOREIGN KEY (""BusinessId"",""ProductSerialId"") REFERENCES product_serials (""BusinessId"",""Id"") NOT VALID;
                ALTER TABLE stock_opname_items VALIDATE CONSTRAINT ""FKT_stock_opname_items_serial"";

                ALTER TABLE product_serials ENABLE ROW LEVEL SECURITY;
                ALTER TABLE product_serials FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON product_serials TO morrus_app
                    USING (""BusinessId""=app_current_business_id()) WITH CHECK (""BusinessId""=app_current_business_id());
                CREATE TRIGGER trg_prevent_business_change BEFORE UPDATE OF ""BusinessId""
                    ON product_serials FOR EACH ROW EXECUTE FUNCTION app_prevent_business_change();

                GRANT SELECT, INSERT, UPDATE, DELETE ON product_serials TO morrus_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS trg_prevent_business_change ON product_serials;
                DROP POLICY IF EXISTS tenant_isolation ON product_serials;

                ALTER TABLE stock_opname_items DROP CONSTRAINT IF EXISTS ""FKT_stock_opname_items_serial"";
                ALTER TABLE stock_opname_items DROP CONSTRAINT IF EXISTS ""FKT_stock_opname_items_batch"";
                ALTER TABLE stock_ledger DROP CONSTRAINT IF EXISTS ""FKT_stock_ledger_serial"";
                ALTER TABLE stock_ledger DROP CONSTRAINT IF EXISTS ""FKT_stock_ledger_batch"";
                ALTER TABLE ""ProductBatches"" DROP CONSTRAINT IF EXISTS ""FKT_ProductBatches_outlet"";
                ALTER TABLE product_serials DROP CONSTRAINT IF EXISTS ""FKT_product_serials_receiving_item"";
                ALTER TABLE product_serials DROP CONSTRAINT IF EXISTS ""FKT_product_serials_outlet"";
                ALTER TABLE product_serials DROP CONSTRAINT IF EXISTS ""FKT_product_serials_variant"";
                ALTER TABLE product_serials DROP CONSTRAINT IF EXISTS ""FKT_product_serials_product"";
                DROP INDEX IF EXISTS ""UXT_product_serials_BusinessId_Id"";
            ");

            migrationBuilder.DropCheckConstraint(
                name: "CK_transaction_items_TrackingModeSnapshot",
                table: "transaction_items");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_outlets_OutletId",
                table: "ProductBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_ledger_ProductBatches_ProductBatchId",
                table: "stock_ledger");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_ledger_product_serials_ProductSerialId",
                table: "stock_ledger");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_opname_items_ProductBatches_ProductBatchId",
                table: "stock_opname_items");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_opname_items_product_serials_ProductSerialId",
                table: "stock_opname_items");

            migrationBuilder.DropTable(
                name: "product_serials");

            migrationBuilder.DropIndex(
                name: "IX_stock_opname_items_ProductBatchId",
                table: "stock_opname_items");

            migrationBuilder.DropIndex(
                name: "IX_stock_opname_items_ProductSerialId",
                table: "stock_opname_items");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_ProductBatchId",
                table: "stock_ledger");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_ProductSerialId",
                table: "stock_ledger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_stock_ledger_TrackingReference",
                table: "stock_ledger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_stock_NonNegative",
                table: "inventory_stock");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_TrackingMode",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_BusinessId_OutletId_ProductId_BatchNumber",
                table: "ProductBatches");

            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_BusinessId_OutletId_ProductId_ProductVariant~",
                table: "ProductBatches");

            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_OutletId_ProductId_ExpiryDate",
                table: "ProductBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductBatches_Qty",
                table: "ProductBatches");

            migrationBuilder.DeleteData(
                table: "industry_profile_capabilities",
                keyColumns: new[] { "CapabilityId", "IndustryProfileId" },
                keyValues: new object[] { new Guid("ca200000-0000-4000-8000-000000000028"), new Guid("ca100001-0000-4000-8000-000000000001") });

            migrationBuilder.DeleteData(
                table: "industry_profile_capabilities",
                keyColumns: new[] { "CapabilityId", "IndustryProfileId" },
                keyValues: new object[] { new Guid("ca200000-0000-4000-8000-000000000028"), new Guid("ca100002-0000-4000-8000-000000000002") });

            migrationBuilder.DeleteData(
                table: "industry_profile_capabilities",
                keyColumns: new[] { "CapabilityId", "IndustryProfileId" },
                keyValues: new object[] { new Guid("ca200000-0000-4000-8000-000000000028"), new Guid("ca100003-0000-4000-8000-000000000003") });

            migrationBuilder.DeleteData(
                table: "industry_profile_capabilities",
                keyColumns: new[] { "CapabilityId", "IndustryProfileId" },
                keyValues: new object[] { new Guid("ca200000-0000-4000-8000-000000000028"), new Guid("ca100004-0000-4000-8000-000000000004") });

            migrationBuilder.DeleteData(
                table: "industry_profile_capabilities",
                keyColumns: new[] { "CapabilityId", "IndustryProfileId" },
                keyValues: new object[] { new Guid("ca200000-0000-4000-8000-000000000028"), new Guid("ca100005-0000-4000-8000-000000000005") });

            migrationBuilder.DeleteData(
                table: "industry_profile_capabilities",
                keyColumns: new[] { "CapabilityId", "IndustryProfileId" },
                keyValues: new object[] { new Guid("ca200000-0000-4000-8000-000000000028"), new Guid("ca100006-0000-4000-8000-000000000006") });

            migrationBuilder.DeleteData(
                table: "industry_profile_capabilities",
                keyColumns: new[] { "CapabilityId", "IndustryProfileId" },
                keyValues: new object[] { new Guid("ca200000-0000-4000-8000-000000000028"), new Guid("ca100007-0000-4000-8000-000000000007") });

            migrationBuilder.DeleteData(
                table: "capabilities",
                keyColumn: "Id",
                keyValue: new Guid("ca200000-0000-4000-8000-000000000028"));

            migrationBuilder.DropColumn(
                name: "TrackingModeSnapshot",
                table: "transaction_items");

            migrationBuilder.DropColumn(
                name: "ProductBatchId",
                table: "stock_opname_items");

            migrationBuilder.DropColumn(
                name: "ProductSerialId",
                table: "stock_opname_items");

            migrationBuilder.DropColumn(
                name: "DocumentItemId",
                table: "stock_ledger");

            migrationBuilder.DropColumn(
                name: "ProductBatchId",
                table: "stock_ledger");

            migrationBuilder.DropColumn(
                name: "ProductSerialId",
                table: "stock_ledger");

            migrationBuilder.DropColumn(
                name: "TrackingMode",
                table: "products");

            migrationBuilder.DropColumn(
                name: "OutletId",
                table: "ProductBatches");

            migrationBuilder.AlterColumn<decimal>(
                name: "QtyRemaining",
                table: "ProductBatches",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "QtyProduction",
                table: "ProductBatches",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpiryDate",
                table: "ProductBatches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BatchNumber",
                table: "ProductBatches",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_BusinessId",
                table: "ProductBatches",
                column: "BusinessId");
        }
    }
}
