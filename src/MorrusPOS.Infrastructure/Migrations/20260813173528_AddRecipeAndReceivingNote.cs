using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeAndReceivingNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductRecipes_products_RawMaterialProductId",
                table: "ProductRecipes");

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityRequired",
                table: "ProductRecipes",
                type: "numeric(10,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductRecipes_products_RawMaterialProductId",
                table: "ProductRecipes",
                column: "RawMaterialProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductRecipes_products_RawMaterialProductId",
                table: "ProductRecipes");

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityRequired",
                table: "ProductRecipes",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,4)");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductRecipes_products_RawMaterialProductId",
                table: "ProductRecipes",
                column: "RawMaterialProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
