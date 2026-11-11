using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStockReservationSerialUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stock_reservation_items_ProductSerialId",
                table: "stock_reservation_items");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservation_items_BusinessId_ProductSerialId_Status",
                table: "stock_reservation_items",
                columns: new[] { "BusinessId", "ProductSerialId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservation_items_ProductSerialId",
                table: "stock_reservation_items",
                column: "ProductSerialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stock_reservation_items_BusinessId_ProductSerialId_Status",
                table: "stock_reservation_items");

            migrationBuilder.DropIndex(
                name: "IX_stock_reservation_items_ProductSerialId",
                table: "stock_reservation_items");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservation_items_ProductSerialId",
                table: "stock_reservation_items",
                column: "ProductSerialId",
                unique: true,
                filter: "\"ProductSerialId\" IS NOT NULL AND \"Status\" = 'active'");
        }
    }
}
