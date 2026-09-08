using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoBizDirectIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gobiz_direct_integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartnerId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    GoBizOutletId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastCatalogPulledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCatalogSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCatalogSyncStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LastCatalogSyncMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastWebhookAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gobiz_direct_integrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gobiz_direct_integrations_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gobiz_direct_integrations_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "gobiz_order_inboxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoBizOrderId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RawPayloadJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gobiz_order_inboxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gobiz_order_inboxes_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gobiz_order_inboxes_transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "gobiz_product_mappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    GoBizItemId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GoBizCategoryId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsSynced = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gobiz_product_mappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gobiz_product_mappings_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gobiz_product_mappings_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gobiz_product_mappings_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gobiz_direct_integrations_BusinessId",
                table: "gobiz_direct_integrations",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_gobiz_direct_integrations_GoBizOutletId",
                table: "gobiz_direct_integrations",
                column: "GoBizOutletId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gobiz_direct_integrations_OutletId",
                table: "gobiz_direct_integrations",
                column: "OutletId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gobiz_order_inboxes_GoBizOrderId",
                table: "gobiz_order_inboxes",
                column: "GoBizOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gobiz_order_inboxes_OutletId",
                table: "gobiz_order_inboxes",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_gobiz_order_inboxes_Status",
                table: "gobiz_order_inboxes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_gobiz_order_inboxes_TransactionId",
                table: "gobiz_order_inboxes",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_gobiz_product_mappings_GoBizItemId",
                table: "gobiz_product_mappings",
                column: "GoBizItemId");

            migrationBuilder.CreateIndex(
                name: "IX_gobiz_product_mappings_OutletId_ProductId_ProductVariantId",
                table: "gobiz_product_mappings",
                columns: new[] { "OutletId", "ProductId", "ProductVariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gobiz_product_mappings_ProductId",
                table: "gobiz_product_mappings",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_gobiz_product_mappings_ProductVariantId",
                table: "gobiz_product_mappings",
                column: "ProductVariantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gobiz_direct_integrations");

            migrationBuilder.DropTable(
                name: "gobiz_order_inboxes");

            migrationBuilder.DropTable(
                name: "gobiz_product_mappings");
        }
    }
}
