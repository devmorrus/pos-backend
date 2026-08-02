using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConsignmentModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consignment_settlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SettlementDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consignment_settlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consignment_settlements_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_consignment_settlements_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "consignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsignmentNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReceiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consignments_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_consignments_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_consignments_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "consignment_sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConsignmentSettlementId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consignment_sales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consignment_sales_consignment_settlements_ConsignmentSettle~",
                        column: x => x.ConsignmentSettlementId,
                        principalTable: "consignment_settlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_consignment_sales_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_consignment_sales_transaction_items_TransactionItemId",
                        column: x => x.TransactionItemId,
                        principalTable: "transaction_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "consignment_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consignment_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consignment_items_consignments_ConsignmentId",
                        column: x => x.ConsignmentId,
                        principalTable: "consignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_consignment_items_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_consignment_items_ConsignmentId",
                table: "consignment_items",
                column: "ConsignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_consignment_items_ProductId",
                table: "consignment_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_consignment_sales_ConsignmentSettlementId",
                table: "consignment_sales",
                column: "ConsignmentSettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_consignment_sales_SupplierId",
                table: "consignment_sales",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_consignment_sales_TransactionItemId",
                table: "consignment_sales",
                column: "TransactionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_consignment_settlements_CreatedBy",
                table: "consignment_settlements",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_consignment_settlements_SettlementNumber",
                table: "consignment_settlements",
                column: "SettlementNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_consignment_settlements_SupplierId",
                table: "consignment_settlements",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_consignments_ConsignmentNumber",
                table: "consignments",
                column: "ConsignmentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_consignments_CreatedBy",
                table: "consignments",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_consignments_OutletId",
                table: "consignments",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_consignments_SupplierId",
                table: "consignments",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consignment_items");

            migrationBuilder.DropTable(
                name: "consignment_sales");

            migrationBuilder.DropTable(
                name: "consignments");

            migrationBuilder.DropTable(
                name: "consignment_settlements");
        }
    }
}
