using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReceivingNotesAndQtyReceived : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_ProductVariants_ProductVariantId",
                table: "ProductBatches");

            migrationBuilder.AddColumn<decimal>(
                name: "QtyReceived",
                table: "purchase_order_items",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductVariantId",
                table: "ProductBatches",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "ProductBatches",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "ReceivingNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivingNumber = table.Column<string>(type: "text", nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ReceivedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivingNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceivingNotes_purchase_orders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReceivingNotes_users_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceivingNoteItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivingNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    QtyReceived = table.Column<decimal>(type: "numeric", nullable: false),
                    BatchNumber = table.Column<string>(type: "text", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivingNoteItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceivingNoteItems_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReceivingNoteItems_ReceivingNotes_ReceivingNoteId",
                        column: x => x.ReceivingNoteId,
                        principalTable: "ReceivingNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReceivingNoteItems_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_ProductId",
                table: "ProductBatches",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingNoteItems_ProductId",
                table: "ReceivingNoteItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingNoteItems_ProductVariantId",
                table: "ReceivingNoteItems",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingNoteItems_ReceivingNoteId",
                table: "ReceivingNoteItems",
                column: "ReceivingNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingNotes_PurchaseOrderId",
                table: "ReceivingNotes",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingNotes_ReceivedByUserId",
                table: "ReceivingNotes",
                column: "ReceivedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_ProductVariants_ProductVariantId",
                table: "ProductBatches",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_products_ProductId",
                table: "ProductBatches",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_ProductVariants_ProductVariantId",
                table: "ProductBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_products_ProductId",
                table: "ProductBatches");

            migrationBuilder.DropTable(
                name: "ReceivingNoteItems");

            migrationBuilder.DropTable(
                name: "ReceivingNotes");

            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_ProductId",
                table: "ProductBatches");

            migrationBuilder.DropColumn(
                name: "QtyReceived",
                table: "purchase_order_items");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "ProductBatches");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductVariantId",
                table: "ProductBatches",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_ProductVariants_ProductVariantId",
                table: "ProductBatches",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
