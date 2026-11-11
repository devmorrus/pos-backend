using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoStageStockTransferReceiving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_idempotency_records_Operation",
                table: "idempotency_records");

            migrationBuilder.AddCheckConstraint(
                name: "CK_idempotency_records_Operation",
                table: "idempotency_records",
                sql: "\"Operation\" IN ('checkout','refund','supplier_payment','stock_transfer_send','stock_transfer_receive')");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "stock_transfers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptNote",
                table: "stock_transfers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedAt",
                table: "stock_transfers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DifferenceQty",
                table: "stock_transfer_items",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedQty",
                table: "stock_transfer_items",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCostSnapshot",
                table: "stock_transfer_items",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VarianceNote",
                table: "stock_transfer_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VarianceReasonCode",
                table: "stock_transfer_items",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql("""
                DO $check$
                BEGIN
                    IF EXISTS (SELECT 1 FROM stock_transfers WHERE "Status" NOT IN ('pending','approved','rejected','cancelled','in_transit','received','received_with_variance','returned')) THEN
                        RAISE EXCEPTION 'STOCK_TRANSFER_STATUS_INVALID: status transfer lama tidak dikenali';
                    END IF;
                END $check$;

                UPDATE stock_transfer_items item
                SET "UnitCostSnapshot" = product."CostPrice"
                FROM products product
                WHERE product."Id" = item."ProductId";

                UPDATE stock_transfer_items item
                SET "UnitCostSnapshot" = variant."CostPrice"
                FROM "ProductVariants" variant
                WHERE variant."Id" = item."ProductVariantId";

                UPDATE stock_transfer_items item
                SET "ReceivedQty" = item."Qty", "DifferenceQty" = 0
                FROM stock_transfers transfer
                WHERE transfer."Id" = item."StockTransferId" AND transfer."Status" IN ('approved','received');

                UPDATE stock_transfer_items item
                SET "ReceivedQty" = 0,
                    "DifferenceQty" = -item."Qty",
                    "VarianceReasonCode" = 'legacy_returned',
                    "VarianceNote" = 'Transfer lama dikembalikan sebelum fitur penerimaan dua tahap.'
                FROM stock_transfers transfer
                WHERE transfer."Id" = item."StockTransferId" AND transfer."Status" IN ('rejected','returned');

                UPDATE stock_transfers SET "ReceivedAt" = "UpdatedAt" WHERE "Status" IN ('approved','rejected','received','returned');
                UPDATE stock_transfers SET "Status" = CASE "Status"
                    WHEN 'pending' THEN 'in_transit'
                    WHEN 'approved' THEN 'received'
                    WHEN 'rejected' THEN 'returned'
                    ELSE "Status" END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_BusinessId_FromStockLocationId_Status_Creat~",
                table: "stock_transfers",
                columns: new[] { "BusinessId", "FromStockLocationId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_BusinessId_ToStockLocationId_Status_Created~",
                table: "stock_transfers",
                columns: new[] { "BusinessId", "ToStockLocationId", "Status", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_stock_transfers_Status",
                table: "stock_transfers",
                sql: "\"Status\" IN ('in_transit','received','received_with_variance','returned','cancelled','pending','approved','rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_stock_transfer_items_Quantities",
                table: "stock_transfer_items",
                sql: "\"Qty\" > 0 AND (\"ReceivedQty\" IS NULL OR (\"ReceivedQty\" >= 0 AND \"DifferenceQty\" = \"ReceivedQty\" - \"Qty\"))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_stock_transfer_items_Variance",
                table: "stock_transfer_items",
                sql: "\"ReceivedQty\" IS NULL OR (\"DifferenceQty\" = 0 AND \"VarianceReasonCode\" IS NULL) OR (\"DifferenceQty\" <> 0 AND \"VarianceReasonCode\" IS NOT NULL AND \"VarianceNote\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_idempotency_records_Operation",
                table: "idempotency_records");

            migrationBuilder.Sql("DELETE FROM idempotency_records WHERE \"Operation\" IN ('stock_transfer_send','stock_transfer_receive');");

            migrationBuilder.AddCheckConstraint(
                name: "CK_idempotency_records_Operation",
                table: "idempotency_records",
                sql: "\"Operation\" IN ('checkout','refund','supplier_payment')");

            migrationBuilder.DropIndex(
                name: "IX_stock_transfers_BusinessId_FromStockLocationId_Status_Creat~",
                table: "stock_transfers");

            migrationBuilder.DropIndex(
                name: "IX_stock_transfers_BusinessId_ToStockLocationId_Status_Created~",
                table: "stock_transfers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_stock_transfers_Status",
                table: "stock_transfers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_stock_transfer_items_Quantities",
                table: "stock_transfer_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_stock_transfer_items_Variance",
                table: "stock_transfer_items");

            migrationBuilder.Sql("""
                UPDATE stock_transfers SET "Status" = CASE "Status"
                    WHEN 'in_transit' THEN 'pending'
                    WHEN 'received' THEN 'approved'
                    WHEN 'received_with_variance' THEN 'approved'
                    WHEN 'returned' THEN 'rejected'
                    ELSE "Status" END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "stock_transfers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.DropColumn(
                name: "ReceiptNote",
                table: "stock_transfers");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                table: "stock_transfers");

            migrationBuilder.DropColumn(
                name: "DifferenceQty",
                table: "stock_transfer_items");

            migrationBuilder.DropColumn(
                name: "ReceivedQty",
                table: "stock_transfer_items");

            migrationBuilder.DropColumn(
                name: "UnitCostSnapshot",
                table: "stock_transfer_items");

            migrationBuilder.DropColumn(
                name: "VarianceNote",
                table: "stock_transfer_items");

            migrationBuilder.DropColumn(
                name: "VarianceReasonCode",
                table: "stock_transfer_items");
        }
    }
}
