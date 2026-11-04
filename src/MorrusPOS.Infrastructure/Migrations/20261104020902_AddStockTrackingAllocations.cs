using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockTrackingAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductBatchId",
                table: "stock_transfer_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductSerialId",
                table: "stock_transfer_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_items_ProductBatchId",
                table: "stock_transfer_items",
                column: "ProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_items_ProductSerialId",
                table: "stock_transfer_items",
                column: "ProductSerialId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_stock_transfer_items_TrackingReference",
                table: "stock_transfer_items",
                sql: "NOT (\"ProductBatchId\" IS NOT NULL AND \"ProductSerialId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_stock_transfer_items_ProductBatches_ProductBatchId",
                table: "stock_transfer_items",
                column: "ProductBatchId",
                principalTable: "ProductBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_transfer_items_product_serials_ProductSerialId",
                table: "stock_transfer_items",
                column: "ProductSerialId",
                principalTable: "product_serials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
                ALTER TABLE stock_transfer_items
                    ADD CONSTRAINT ""FKT_stock_transfer_items_batch""
                    FOREIGN KEY (""BusinessId"",""ProductBatchId"") REFERENCES ""ProductBatches"" (""BusinessId"",""Id"") NOT VALID;
                ALTER TABLE stock_transfer_items VALIDATE CONSTRAINT ""FKT_stock_transfer_items_batch"";
                ALTER TABLE stock_transfer_items
                    ADD CONSTRAINT ""FKT_stock_transfer_items_serial""
                    FOREIGN KEY (""BusinessId"",""ProductSerialId"") REFERENCES product_serials (""BusinessId"",""Id"") NOT VALID;
                ALTER TABLE stock_transfer_items VALIDATE CONSTRAINT ""FKT_stock_transfer_items_serial"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE stock_transfer_items DROP CONSTRAINT IF EXISTS ""FKT_stock_transfer_items_serial"";
                ALTER TABLE stock_transfer_items DROP CONSTRAINT IF EXISTS ""FKT_stock_transfer_items_batch"";
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_transfer_items_ProductBatches_ProductBatchId",
                table: "stock_transfer_items");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_transfer_items_product_serials_ProductSerialId",
                table: "stock_transfer_items");

            migrationBuilder.DropIndex(
                name: "IX_stock_transfer_items_ProductBatchId",
                table: "stock_transfer_items");

            migrationBuilder.DropIndex(
                name: "IX_stock_transfer_items_ProductSerialId",
                table: "stock_transfer_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_stock_transfer_items_TrackingReference",
                table: "stock_transfer_items");

            migrationBuilder.DropColumn(
                name: "ProductBatchId",
                table: "stock_transfer_items");

            migrationBuilder.DropColumn(
                name: "ProductSerialId",
                table: "stock_transfer_items");
        }
    }
}
