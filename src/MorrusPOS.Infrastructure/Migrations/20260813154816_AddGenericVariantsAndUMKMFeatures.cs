using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGenericVariantsAndUMKMFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                table: "transaction_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedModifiersJson",
                table: "transaction_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                table: "supplier_return_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                table: "stock_transfer_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                table: "stock_opname_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                table: "stock_ledger",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                table: "purchase_order_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasVariants",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRawMaterial",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                table: "inventory_stock",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                table: "consignment_return_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                table: "consignment_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ModifierGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    MinSelection = table.Column<int>(type: "integer", nullable: false),
                    MaxSelection = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModifierGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModifierGroups_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductAttributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductAttributes_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    Barcode = table.Column<string>(type: "text", nullable: true),
                    BasePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    CostPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductVariants_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModifierGroupProduct",
                columns: table => new
                {
                    ModifierGroupsId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModifierGroupProduct", x => new { x.ModifierGroupsId, x.ProductsId });
                    table.ForeignKey(
                        name: "FK_ModifierGroupProduct_ModifierGroups_ModifierGroupsId",
                        column: x => x.ModifierGroupsId,
                        principalTable: "ModifierGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModifierGroupProduct_products_ProductsId",
                        column: x => x.ProductsId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModifierOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifierGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ExtraPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    ExtraCost = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModifierOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModifierOptions_ModifierGroups_ModifierGroupId",
                        column: x => x.ModifierGroupId,
                        principalTable: "ModifierGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductAttributeValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAttributeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductAttributeValues_ProductAttributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "ProductAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductAttributeValues_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchNumber = table.Column<string>(type: "text", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    QtyProduction = table.Column<decimal>(type: "numeric", nullable: false),
                    QtyRemaining = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductBatches_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductRecipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RawMaterialProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityRequired = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductRecipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductRecipes_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductRecipes_products_RawMaterialProductId",
                        column: x => x.RawMaterialProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transaction_items_ProductVariantId",
                table: "transaction_items",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_return_items_ProductVariantId",
                table: "supplier_return_items",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_items_ProductVariantId",
                table: "stock_transfer_items",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_opname_items_ProductVariantId",
                table: "stock_opname_items",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_ProductVariantId",
                table: "stock_ledger",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_items_ProductVariantId",
                table: "purchase_order_items",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stock_ProductVariantId",
                table: "inventory_stock",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_consignment_return_items_ProductVariantId",
                table: "consignment_return_items",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_consignment_items_ProductVariantId",
                table: "consignment_items",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierGroupProduct_ProductsId",
                table: "ModifierGroupProduct",
                column: "ProductsId");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierGroups_BusinessId",
                table: "ModifierGroups",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierOptions_ModifierGroupId",
                table: "ModifierOptions",
                column: "ModifierGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributes_BusinessId",
                table: "ProductAttributes",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributeValues_AttributeId",
                table: "ProductAttributeValues",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributeValues_ProductVariantId",
                table: "ProductAttributeValues",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_ProductVariantId",
                table: "ProductBatches",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRecipes_ProductVariantId",
                table: "ProductRecipes",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRecipes_RawMaterialProductId",
                table: "ProductRecipes",
                column: "RawMaterialProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId",
                table: "ProductVariants",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_consignment_items_ProductVariants_ProductVariantId",
                table: "consignment_items",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_consignment_return_items_ProductVariants_ProductVariantId",
                table: "consignment_return_items",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_stock_ProductVariants_ProductVariantId",
                table: "inventory_stock",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_order_items_ProductVariants_ProductVariantId",
                table: "purchase_order_items",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_stock_ledger_ProductVariants_ProductVariantId",
                table: "stock_ledger",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_stock_opname_items_ProductVariants_ProductVariantId",
                table: "stock_opname_items",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_stock_transfer_items_ProductVariants_ProductVariantId",
                table: "stock_transfer_items",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_return_items_ProductVariants_ProductVariantId",
                table: "supplier_return_items",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_transaction_items_ProductVariants_ProductVariantId",
                table: "transaction_items",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_consignment_items_ProductVariants_ProductVariantId",
                table: "consignment_items");

            migrationBuilder.DropForeignKey(
                name: "FK_consignment_return_items_ProductVariants_ProductVariantId",
                table: "consignment_return_items");

            migrationBuilder.DropForeignKey(
                name: "FK_inventory_stock_ProductVariants_ProductVariantId",
                table: "inventory_stock");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_order_items_ProductVariants_ProductVariantId",
                table: "purchase_order_items");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_ledger_ProductVariants_ProductVariantId",
                table: "stock_ledger");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_opname_items_ProductVariants_ProductVariantId",
                table: "stock_opname_items");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_transfer_items_ProductVariants_ProductVariantId",
                table: "stock_transfer_items");

            migrationBuilder.DropForeignKey(
                name: "FK_supplier_return_items_ProductVariants_ProductVariantId",
                table: "supplier_return_items");

            migrationBuilder.DropForeignKey(
                name: "FK_transaction_items_ProductVariants_ProductVariantId",
                table: "transaction_items");

            migrationBuilder.DropTable(
                name: "ModifierGroupProduct");

            migrationBuilder.DropTable(
                name: "ModifierOptions");

            migrationBuilder.DropTable(
                name: "ProductAttributeValues");

            migrationBuilder.DropTable(
                name: "ProductBatches");

            migrationBuilder.DropTable(
                name: "ProductRecipes");

            migrationBuilder.DropTable(
                name: "ModifierGroups");

            migrationBuilder.DropTable(
                name: "ProductAttributes");

            migrationBuilder.DropTable(
                name: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_transaction_items_ProductVariantId",
                table: "transaction_items");

            migrationBuilder.DropIndex(
                name: "IX_supplier_return_items_ProductVariantId",
                table: "supplier_return_items");

            migrationBuilder.DropIndex(
                name: "IX_stock_transfer_items_ProductVariantId",
                table: "stock_transfer_items");

            migrationBuilder.DropIndex(
                name: "IX_stock_opname_items_ProductVariantId",
                table: "stock_opname_items");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_ProductVariantId",
                table: "stock_ledger");

            migrationBuilder.DropIndex(
                name: "IX_purchase_order_items_ProductVariantId",
                table: "purchase_order_items");

            migrationBuilder.DropIndex(
                name: "IX_inventory_stock_ProductVariantId",
                table: "inventory_stock");

            migrationBuilder.DropIndex(
                name: "IX_consignment_return_items_ProductVariantId",
                table: "consignment_return_items");

            migrationBuilder.DropIndex(
                name: "IX_consignment_items_ProductVariantId",
                table: "consignment_items");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "transaction_items");

            migrationBuilder.DropColumn(
                name: "SelectedModifiersJson",
                table: "transaction_items");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "supplier_return_items");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "stock_transfer_items");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "stock_opname_items");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "stock_ledger");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "purchase_order_items");

            migrationBuilder.DropColumn(
                name: "HasVariants",
                table: "products");

            migrationBuilder.DropColumn(
                name: "IsRawMaterial",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "inventory_stock");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "consignment_return_items");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "consignment_items");
        }
    }
}
