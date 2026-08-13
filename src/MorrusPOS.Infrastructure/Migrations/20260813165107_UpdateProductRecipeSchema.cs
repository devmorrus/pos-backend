using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProductRecipeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductRecipes_ProductVariants_ProductVariantId",
                table: "ProductRecipes");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductVariantId",
                table: "ProductRecipes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "ProductRecipes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ProductRecipes_ProductId",
                table: "ProductRecipes",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductRecipes_ProductVariants_ProductVariantId",
                table: "ProductRecipes",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductRecipes_products_ProductId",
                table: "ProductRecipes",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductRecipes_ProductVariants_ProductVariantId",
                table: "ProductRecipes");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductRecipes_products_ProductId",
                table: "ProductRecipes");

            migrationBuilder.DropIndex(
                name: "IX_ProductRecipes_ProductId",
                table: "ProductRecipes");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "ProductRecipes");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductVariantId",
                table: "ProductRecipes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductRecipes_ProductVariants_ProductVariantId",
                table: "ProductRecipes",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
