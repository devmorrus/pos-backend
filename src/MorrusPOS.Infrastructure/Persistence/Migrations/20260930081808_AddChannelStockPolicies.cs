using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelStockPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "channel_stock_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BufferQty = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channel_stock_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_channel_stock_policies_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_channel_stock_policies_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_channel_stock_policies_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_channel_stock_policies_users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_channel_stock_policies_OutletId",
                table: "channel_stock_policies",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_channel_stock_policies_OutletId_ProductId",
                table: "channel_stock_policies",
                columns: new[] { "OutletId", "ProductId" },
                unique: true,
                filter: "\"ProductVariantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_channel_stock_policies_OutletId_ProductId_ProductVariantId",
                table: "channel_stock_policies",
                columns: new[] { "OutletId", "ProductId", "ProductVariantId" },
                unique: true,
                filter: "\"ProductVariantId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_channel_stock_policies_ProductId",
                table: "channel_stock_policies",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_channel_stock_policies_ProductVariantId",
                table: "channel_stock_policies",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_channel_stock_policies_UpdatedBy",
                table: "channel_stock_policies",
                column: "UpdatedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channel_stock_policies");
        }
    }
}
