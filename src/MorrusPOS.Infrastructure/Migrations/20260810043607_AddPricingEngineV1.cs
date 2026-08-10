using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingEngineV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppliedPromoName",
                table: "transactions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppliedVoucherCode",
                table: "transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ManualDiscountTotal",
                table: "transactions",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PromoDiscountTotal",
                table: "transactions",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceChargeTotal",
                table: "transactions",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VoucherDiscountTotal",
                table: "transactions",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsServiceChargeable",
                table: "products",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTaxable",
                table: "products",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "promo_campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DiscountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    ScopeType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MinimumSpend = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    MaximumDiscountAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promo_campaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_promo_campaigns_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_charge_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(8,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_charge_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_charge_rules_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tax_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(8,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AppliesBeforeServiceCharge = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tax_rules_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DiscountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    MinimumSpend = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    MaximumDiscountAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    UsageLimitTotal = table.Column<int>(type: "integer", nullable: false),
                    UsageLimitPerCode = table.Column<int>(type: "integer", nullable: false),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vouchers_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "promo_campaign_targets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PromoCampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promo_campaign_targets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_promo_campaign_targets_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_promo_campaign_targets_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_promo_campaign_targets_promo_campaigns_PromoCampaignId",
                        column: x => x.PromoCampaignId,
                        principalTable: "promo_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "voucher_redemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RedeemedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RedeemedAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_redemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_voucher_redemptions_transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_voucher_redemptions_vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "Id", "Code", "Description" },
                values: new object[] { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "pricing.manage", "Mengelola pricing rule, promo, voucher, pajak, dan service charge" });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") },
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_promo_campaign_targets_CategoryId",
                table: "promo_campaign_targets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_promo_campaign_targets_ProductId",
                table: "promo_campaign_targets",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_promo_campaign_targets_PromoCampaignId_ProductId_CategoryId",
                table: "promo_campaign_targets",
                columns: new[] { "PromoCampaignId", "ProductId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_promo_campaigns_OutletId_Code",
                table: "promo_campaigns",
                columns: new[] { "OutletId", "Code" },
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_promo_campaigns_OutletId_IsActive_StartAt_EndAt",
                table: "promo_campaigns",
                columns: new[] { "OutletId", "IsActive", "StartAt", "EndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_service_charge_rules_OutletId_IsActive_UpdatedAt",
                table: "service_charge_rules",
                columns: new[] { "OutletId", "IsActive", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_rules_OutletId_IsActive_UpdatedAt",
                table: "tax_rules",
                columns: new[] { "OutletId", "IsActive", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_voucher_redemptions_TransactionId",
                table: "voucher_redemptions",
                column: "TransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_redemptions_VoucherId_RedeemedAt",
                table: "voucher_redemptions",
                columns: new[] { "VoucherId", "RedeemedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_OutletId_Code",
                table: "vouchers",
                columns: new[] { "OutletId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_OutletId_IsActive_StartAt_EndAt",
                table: "vouchers",
                columns: new[] { "OutletId", "IsActive", "StartAt", "EndAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "promo_campaign_targets");

            migrationBuilder.DropTable(
                name: "service_charge_rules");

            migrationBuilder.DropTable(
                name: "tax_rules");

            migrationBuilder.DropTable(
                name: "voucher_redemptions");

            migrationBuilder.DropTable(
                name: "promo_campaigns");

            migrationBuilder.DropTable(
                name: "vouchers");

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

            migrationBuilder.DropColumn(
                name: "AppliedPromoName",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "AppliedVoucherCode",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "ManualDiscountTotal",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "PromoDiscountTotal",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "ServiceChargeTotal",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "VoucherDiscountTotal",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "IsServiceChargeable",
                table: "products");

            migrationBuilder.DropColumn(
                name: "IsTaxable",
                table: "products");
        }
    }
}
