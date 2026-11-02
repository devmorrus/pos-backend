using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilitiesAndIndustryProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CapabilityRevision",
                table: "businesses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "IndustryProfileId",
                table: "businesses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "capabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsTenantConfigurable = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capabilities", x => x.Id);
                    table.CheckConstraint("CK_capabilities_Scope", "\"Scope\" IN ('Business','Outlet')");
                });

            migrationBuilder.CreateTable(
                name: "industry_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_industry_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "business_capability_overrides",
                columns: table => new
                {
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapabilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_capability_overrides", x => new { x.BusinessId, x.CapabilityId });
                    table.ForeignKey(
                        name: "FK_business_capability_overrides_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_business_capability_overrides_capabilities_CapabilityId",
                        column: x => x.CapabilityId,
                        principalTable: "capabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_business_capability_overrides_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "outlet_capability_overrides",
                columns: table => new
                {
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapabilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outlet_capability_overrides", x => new { x.BusinessId, x.OutletId, x.CapabilityId });
                    table.ForeignKey(
                        name: "FK_outlet_capability_overrides_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_outlet_capability_overrides_capabilities_CapabilityId",
                        column: x => x.CapabilityId,
                        principalTable: "capabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_outlet_capability_overrides_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_outlet_capability_overrides_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "capability_change_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    CapabilityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangeType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OldValue = table.Column<bool>(type: "boolean", nullable: true),
                    NewValue = table.Column<bool>(type: "boolean", nullable: true),
                    OldProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capability_change_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_capability_change_logs_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_capability_change_logs_capabilities_CapabilityId",
                        column: x => x.CapabilityId,
                        principalTable: "capabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_capability_change_logs_industry_profiles_NewProfileId",
                        column: x => x.NewProfileId,
                        principalTable: "industry_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_capability_change_logs_industry_profiles_OldProfileId",
                        column: x => x.OldProfileId,
                        principalTable: "industry_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_capability_change_logs_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_capability_change_logs_users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "industry_profile_capabilities",
                columns: table => new
                {
                    IndustryProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapabilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_industry_profile_capabilities", x => new { x.IndustryProfileId, x.CapabilityId });
                    table.ForeignKey(
                        name: "FK_industry_profile_capabilities_capabilities_CapabilityId",
                        column: x => x.CapabilityId,
                        principalTable: "capabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_industry_profile_capabilities_industry_profiles_IndustryPro~",
                        column: x => x.IndustryProfileId,
                        principalTable: "industry_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "businesses",
                keyColumn: "Id",
                keyValue: new Guid("11111111-2222-3333-4444-555555555555"),
                columns: new[] { "CapabilityRevision", "IndustryProfileId" },
                values: new object[] { 0L, new Guid("ca100003-0000-4000-8000-000000000003") });

            migrationBuilder.InsertData(
                table: "capabilities",
                columns: new[] { "Id", "Code", "CreatedAt", "DefaultEnabled", "Description", "IsActive", "IsTenantConfigurable", "Module", "Name", "Scope", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("ca200000-0000-4000-8000-000000000001"), "sales.pos", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, "POS Kasir", true, true, "Sales", "POS Kasir", "Outlet", 1, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000002"), "sales.transactions", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, "Riwayat Transaksi", true, true, "Sales", "Riwayat Transaksi", "Outlet", 2, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000003"), "customers", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, "Customer & Member", true, true, "Sales", "Customer & Member", "Business", 3, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000004"), "catalog.products", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, "Produk & Kategori", true, true, "Catalog", "Produk & Kategori", "Business", 4, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000005"), "catalog.variants", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Varian Produk", true, true, "Catalog", "Varian Produk", "Business", 5, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000006"), "catalog.modifiers", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Modifier Produk", true, true, "Catalog", "Modifier Produk", "Business", 6, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000007"), "catalog.recipes", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Resep / BOM", true, true, "Catalog", "Resep / BOM", "Business", 7, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000008"), "catalog.batch_expiry", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Batch & Kedaluwarsa", true, true, "Catalog", "Batch & Kedaluwarsa", "Business", 8, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000009"), "inventory.stock", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, "Manajemen Stok", true, true, "Inventory", "Manajemen Stok", "Outlet", 9, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000010"), "inventory.buffer_stock", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Buffer Stock", true, true, "Inventory", "Buffer Stock", "Outlet", 10, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000011"), "inventory.opname", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, "Stock Opname", true, true, "Inventory", "Stock Opname", "Outlet", 11, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000012"), "inventory.transfer", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Transfer Stok", true, true, "Inventory", "Transfer Stok", "Outlet", 12, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000013"), "procurement.purchasing", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Supplier & Pembelian", true, true, "Procurement", "Supplier & Pembelian", "Outlet", 13, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000014"), "procurement.supplier_debt", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Utang Supplier", true, true, "Procurement", "Utang Supplier", "Business", 14, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000015"), "procurement.supplier_return", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Retur Supplier", true, true, "Procurement", "Retur Supplier", "Outlet", 15, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000016"), "consignment", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Konsinyasi", true, true, "Procurement", "Konsinyasi", "Outlet", 16, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000017"), "channels.settlement", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Channel Settlement", true, true, "Channels", "Channel Settlement", "Outlet", 17, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000018"), "channels.gobiz", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Integrasi GoBiz", true, true, "Channels", "Integrasi GoBiz", "Outlet", 18, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000019"), "pricing.tax_service", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Pajak & Service Charge", true, true, "Pricing", "Pajak & Service Charge", "Outlet", 19, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000020"), "pricing.promotions", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Promo Campaign", true, true, "Pricing", "Promo Campaign", "Outlet", 20, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000021"), "pricing.vouchers", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Voucher", true, true, "Pricing", "Voucher", "Outlet", 21, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000022"), "accounting.core", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Akuntansi", true, true, "Accounting", "Akuntansi", "Business", 22, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000023"), "accounting.cashflow", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Arus Kas Manual", true, true, "Accounting", "Arus Kas Manual", "Business", 23, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000024"), "accounting.integration", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Integrasi Akuntansi", true, true, "Accounting", "Integrasi Akuntansi", "Business", 24, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000025"), "reports.operational", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), true, "Laporan Operasional", true, true, "Reports", "Laporan Operasional", "Business", 25, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000026"), "reports.accounting", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Laporan Akuntansi", true, true, "Reports", "Laporan Akuntansi", "Business", 26, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca200000-0000-4000-8000-000000000027"), "storefront.public", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), false, "Public Storefront", true, true, "Channels", "Public Storefront", "Outlet", 27, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "industry_profiles",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "IsActive", "Name", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("ca100001-0000-4000-8000-000000000001"), "general", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Konfigurasi umum untuk bisnis lintas industri.", true, "General", 1, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca100002-0000-4000-8000-000000000002"), "food-beverage", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Kafe, restoran, bakery, dan usaha kuliner.", true, "Food & Beverage", 2, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca100003-0000-4000-8000-000000000003"), "retail", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Toko retail dan perdagangan umum.", true, "Retail", 3, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca100004-0000-4000-8000-000000000004"), "grocery", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Minimarket, sembako, dan produk dengan batch/expiry.", true, "Grocery", 4, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca100005-0000-4000-8000-000000000005"), "fashion", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Butik, pakaian, dan produk dengan varian.", true, "Fashion", 5, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ca100006-0000-4000-8000-000000000006"), "services", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Salon, laundry, dan bisnis jasa.", true, "Services", 6, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "Id", "Code", "Description" },
                values: new object[,]
                {
                    { new Guid("14141414-1414-1414-1414-141414141414"), "capability.view", "Melihat profil industri dan capability" },
                    { new Guid("15151515-1515-1515-1515-151515151515"), "capability.manage", "Mengubah profil industri dan capability" }
                });

            migrationBuilder.InsertData(
                table: "industry_profile_capabilities",
                columns: new[] { "CapabilityId", "IndustryProfileId", "IsEnabled" },
                values: new object[,]
                {
                    { new Guid("ca200000-0000-4000-8000-000000000001"), new Guid("ca100001-0000-4000-8000-000000000001"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000002"), new Guid("ca100001-0000-4000-8000-000000000001"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000003"), new Guid("ca100001-0000-4000-8000-000000000001"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000004"), new Guid("ca100001-0000-4000-8000-000000000001"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000005"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000006"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000007"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000008"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000009"), new Guid("ca100001-0000-4000-8000-000000000001"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000010"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000011"), new Guid("ca100001-0000-4000-8000-000000000001"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000012"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000013"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000014"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000015"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000016"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000017"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000018"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000019"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000020"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000021"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000022"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000023"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000024"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000025"), new Guid("ca100001-0000-4000-8000-000000000001"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000026"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000027"), new Guid("ca100001-0000-4000-8000-000000000001"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000001"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000002"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000003"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000004"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000005"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000006"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000007"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000008"), new Guid("ca100002-0000-4000-8000-000000000002"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000009"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000010"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000011"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000012"), new Guid("ca100002-0000-4000-8000-000000000002"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000013"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000014"), new Guid("ca100002-0000-4000-8000-000000000002"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000015"), new Guid("ca100002-0000-4000-8000-000000000002"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000016"), new Guid("ca100002-0000-4000-8000-000000000002"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000017"), new Guid("ca100002-0000-4000-8000-000000000002"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000018"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000019"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000020"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000021"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000022"), new Guid("ca100002-0000-4000-8000-000000000002"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000023"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000024"), new Guid("ca100002-0000-4000-8000-000000000002"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000025"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000026"), new Guid("ca100002-0000-4000-8000-000000000002"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000027"), new Guid("ca100002-0000-4000-8000-000000000002"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000001"), new Guid("ca100003-0000-4000-8000-000000000003"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000002"), new Guid("ca100003-0000-4000-8000-000000000003"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000003"), new Guid("ca100003-0000-4000-8000-000000000003"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000004"), new Guid("ca100003-0000-4000-8000-000000000003"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000005"), new Guid("ca100003-0000-4000-8000-000000000003"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000006"), new Guid("ca100003-0000-4000-8000-000000000003"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000007"), new Guid("ca100003-0000-4000-8000-000000000003"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000008"), new Guid("ca100003-0000-4000-8000-000000000003"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000009"), new Guid("ca100003-0000-4000-8000-000000000003"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000010"), new Guid("ca100003-0000-4000-8000-000000000003"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000011"), new Guid("ca100003-0000-4000-8000-000000000003"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000012"), new Guid("ca100003-0000-4000-8000-000000000003"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000013"), new Guid("ca100003-0000-4000-8000-000000000003"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000014"), new Guid("ca100003-0000-4000-8000-000000000003"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000015"), new Guid("ca100003-0000-4000-8000-000000000003"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000016"), new Guid("ca100003-0000-4000-8000-000000000003"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000017"), new Guid("ca100003-0000-4000-8000-000000000003"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000018"), new Guid("ca100003-0000-4000-8000-000000000003"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000019"), new Guid("ca100003-0000-4000-8000-000000000003"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000020"), new Guid("ca100003-0000-4000-8000-000000000003"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000021"), new Guid("ca100003-0000-4000-8000-000000000003"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000022"), new Guid("ca100003-0000-4000-8000-000000000003"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000023"), new Guid("ca100003-0000-4000-8000-000000000003"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000024"), new Guid("ca100003-0000-4000-8000-000000000003"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000025"), new Guid("ca100003-0000-4000-8000-000000000003"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000026"), new Guid("ca100003-0000-4000-8000-000000000003"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000027"), new Guid("ca100003-0000-4000-8000-000000000003"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000001"), new Guid("ca100004-0000-4000-8000-000000000004"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000002"), new Guid("ca100004-0000-4000-8000-000000000004"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000003"), new Guid("ca100004-0000-4000-8000-000000000004"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000004"), new Guid("ca100004-0000-4000-8000-000000000004"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000005"), new Guid("ca100004-0000-4000-8000-000000000004"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000006"), new Guid("ca100004-0000-4000-8000-000000000004"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000007"), new Guid("ca100004-0000-4000-8000-000000000004"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000008"), new Guid("ca100004-0000-4000-8000-000000000004"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000009"), new Guid("ca100004-0000-4000-8000-000000000004"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000010"), new Guid("ca100004-0000-4000-8000-000000000004"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000011"), new Guid("ca100004-0000-4000-8000-000000000004"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000012"), new Guid("ca100004-0000-4000-8000-000000000004"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000013"), new Guid("ca100004-0000-4000-8000-000000000004"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000014"), new Guid("ca100004-0000-4000-8000-000000000004"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000015"), new Guid("ca100004-0000-4000-8000-000000000004"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000016"), new Guid("ca100004-0000-4000-8000-000000000004"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000017"), new Guid("ca100004-0000-4000-8000-000000000004"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000018"), new Guid("ca100004-0000-4000-8000-000000000004"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000019"), new Guid("ca100004-0000-4000-8000-000000000004"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000020"), new Guid("ca100004-0000-4000-8000-000000000004"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000021"), new Guid("ca100004-0000-4000-8000-000000000004"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000022"), new Guid("ca100004-0000-4000-8000-000000000004"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000023"), new Guid("ca100004-0000-4000-8000-000000000004"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000024"), new Guid("ca100004-0000-4000-8000-000000000004"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000025"), new Guid("ca100004-0000-4000-8000-000000000004"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000026"), new Guid("ca100004-0000-4000-8000-000000000004"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000027"), new Guid("ca100004-0000-4000-8000-000000000004"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000001"), new Guid("ca100005-0000-4000-8000-000000000005"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000002"), new Guid("ca100005-0000-4000-8000-000000000005"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000003"), new Guid("ca100005-0000-4000-8000-000000000005"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000004"), new Guid("ca100005-0000-4000-8000-000000000005"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000005"), new Guid("ca100005-0000-4000-8000-000000000005"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000006"), new Guid("ca100005-0000-4000-8000-000000000005"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000007"), new Guid("ca100005-0000-4000-8000-000000000005"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000008"), new Guid("ca100005-0000-4000-8000-000000000005"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000009"), new Guid("ca100005-0000-4000-8000-000000000005"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000010"), new Guid("ca100005-0000-4000-8000-000000000005"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000011"), new Guid("ca100005-0000-4000-8000-000000000005"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000012"), new Guid("ca100005-0000-4000-8000-000000000005"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000013"), new Guid("ca100005-0000-4000-8000-000000000005"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000014"), new Guid("ca100005-0000-4000-8000-000000000005"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000015"), new Guid("ca100005-0000-4000-8000-000000000005"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000016"), new Guid("ca100005-0000-4000-8000-000000000005"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000017"), new Guid("ca100005-0000-4000-8000-000000000005"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000018"), new Guid("ca100005-0000-4000-8000-000000000005"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000019"), new Guid("ca100005-0000-4000-8000-000000000005"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000020"), new Guid("ca100005-0000-4000-8000-000000000005"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000021"), new Guid("ca100005-0000-4000-8000-000000000005"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000022"), new Guid("ca100005-0000-4000-8000-000000000005"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000023"), new Guid("ca100005-0000-4000-8000-000000000005"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000024"), new Guid("ca100005-0000-4000-8000-000000000005"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000025"), new Guid("ca100005-0000-4000-8000-000000000005"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000026"), new Guid("ca100005-0000-4000-8000-000000000005"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000027"), new Guid("ca100005-0000-4000-8000-000000000005"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000001"), new Guid("ca100006-0000-4000-8000-000000000006"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000002"), new Guid("ca100006-0000-4000-8000-000000000006"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000003"), new Guid("ca100006-0000-4000-8000-000000000006"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000004"), new Guid("ca100006-0000-4000-8000-000000000006"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000005"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000006"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000007"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000008"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000009"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000010"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000011"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000012"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000013"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000014"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000015"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000016"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000017"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000018"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000019"), new Guid("ca100006-0000-4000-8000-000000000006"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000020"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000021"), new Guid("ca100006-0000-4000-8000-000000000006"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000022"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000023"), new Guid("ca100006-0000-4000-8000-000000000006"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000024"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000025"), new Guid("ca100006-0000-4000-8000-000000000006"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000026"), new Guid("ca100006-0000-4000-8000-000000000006"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000027"), new Guid("ca100006-0000-4000-8000-000000000006"), false }
                });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("14141414-1414-1414-1414-141414141414"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("14141414-1414-1414-1414-141414141414"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") },
                    { new Guid("15151515-1515-1515-1515-151515151515"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") }
                });

            // Existing tenants keep all current modules enabled. New tenants
            // inherit the selected industry profile without compatibility rows.
            migrationBuilder.Sql(@"
                UPDATE businesses
                SET ""IndustryProfileId"" = CASE lower(coalesce(""Category"",''))
                    WHEN 'food & beverage' THEN 'ca100002-0000-4000-8000-000000000002'::uuid
                    WHEN 'f&b' THEN 'ca100002-0000-4000-8000-000000000002'::uuid
                    WHEN 'retail' THEN 'ca100003-0000-4000-8000-000000000003'::uuid
                    WHEN 'grocery' THEN 'ca100004-0000-4000-8000-000000000004'::uuid
                    WHEN 'fashion' THEN 'ca100005-0000-4000-8000-000000000005'::uuid
                    WHEN 'services' THEN 'ca100006-0000-4000-8000-000000000006'::uuid
                    ELSE 'ca100001-0000-4000-8000-000000000001'::uuid
                END
                WHERE ""IndustryProfileId"" IS NULL;

                DO $owner_guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM businesses WHERE ""OwnerId"" IS NULL) THEN
                        RAISE EXCEPTION 'Capability migration requires OwnerId for every existing Business';
                    END IF;
                END
                $owner_guard$;

                INSERT INTO business_capability_overrides
                    (""BusinessId"",""CapabilityId"",""IsEnabled"",""Reason"",""UpdatedByUserId"",""CreatedAt"",""UpdatedAt"")
                SELECT b.""Id"",c.""Id"",true,'Compatibility rollout',b.""OwnerId"",now(),now()
                FROM businesses b CROSS JOIN capabilities c
                ON CONFLICT DO NOTHING;

                ALTER TABLE businesses ALTER COLUMN ""IndustryProfileId"" SET NOT NULL;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_businesses_IndustryProfileId",
                table: "businesses",
                column: "IndustryProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_business_capability_overrides_CapabilityId",
                table: "business_capability_overrides",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_business_capability_overrides_UpdatedByUserId",
                table: "business_capability_overrides",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_capabilities_Code",
                table: "capabilities",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_capabilities_Module_SortOrder",
                table: "capabilities",
                columns: new[] { "Module", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_capability_change_logs_BusinessId_CapabilityId_CreatedAt",
                table: "capability_change_logs",
                columns: new[] { "BusinessId", "CapabilityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_capability_change_logs_BusinessId_CreatedAt",
                table: "capability_change_logs",
                columns: new[] { "BusinessId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_capability_change_logs_BusinessId_OutletId_CreatedAt",
                table: "capability_change_logs",
                columns: new[] { "BusinessId", "OutletId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_capability_change_logs_CapabilityId",
                table: "capability_change_logs",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_capability_change_logs_ChangedByUserId",
                table: "capability_change_logs",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_capability_change_logs_NewProfileId",
                table: "capability_change_logs",
                column: "NewProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_capability_change_logs_OldProfileId",
                table: "capability_change_logs",
                column: "OldProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_capability_change_logs_OutletId",
                table: "capability_change_logs",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_industry_profile_capabilities_CapabilityId",
                table: "industry_profile_capabilities",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_industry_profiles_Code",
                table: "industry_profiles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outlet_capability_overrides_CapabilityId",
                table: "outlet_capability_overrides",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_outlet_capability_overrides_OutletId",
                table: "outlet_capability_overrides",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_outlet_capability_overrides_UpdatedByUserId",
                table: "outlet_capability_overrides",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_businesses_industry_profiles_IndustryProfileId",
                table: "businesses",
                column: "IndustryProfileId",
                principalTable: "industry_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""UXT_capability_logs_BusinessId_Id""
                    ON capability_change_logs (""BusinessId"",""Id"");

                ALTER TABLE business_capability_overrides
                    ADD CONSTRAINT ""FKT_business_capability_updated_by""
                    FOREIGN KEY (""BusinessId"",""UpdatedByUserId"") REFERENCES users (""BusinessId"",""Id"") NOT VALID;
                ALTER TABLE business_capability_overrides VALIDATE CONSTRAINT ""FKT_business_capability_updated_by"";
                ALTER TABLE outlet_capability_overrides
                    ADD CONSTRAINT ""FKT_outlet_capability_outlet""
                    FOREIGN KEY (""BusinessId"",""OutletId"") REFERENCES outlets (""BusinessId"",""Id"") NOT VALID;
                ALTER TABLE outlet_capability_overrides VALIDATE CONSTRAINT ""FKT_outlet_capability_outlet"";
                ALTER TABLE outlet_capability_overrides
                    ADD CONSTRAINT ""FKT_outlet_capability_updated_by""
                    FOREIGN KEY (""BusinessId"",""UpdatedByUserId"") REFERENCES users (""BusinessId"",""Id"") NOT VALID;
                ALTER TABLE outlet_capability_overrides VALIDATE CONSTRAINT ""FKT_outlet_capability_updated_by"";
                ALTER TABLE capability_change_logs
                    ADD CONSTRAINT ""FKT_capability_log_outlet""
                    FOREIGN KEY (""BusinessId"",""OutletId"") REFERENCES outlets (""BusinessId"",""Id"") NOT VALID;
                ALTER TABLE capability_change_logs VALIDATE CONSTRAINT ""FKT_capability_log_outlet"";
                ALTER TABLE capability_change_logs
                    ADD CONSTRAINT ""FKT_capability_log_user""
                    FOREIGN KEY (""BusinessId"",""ChangedByUserId"") REFERENCES users (""BusinessId"",""Id"") NOT VALID;
                ALTER TABLE capability_change_logs VALIDATE CONSTRAINT ""FKT_capability_log_user"";

                ALTER TABLE business_capability_overrides ENABLE ROW LEVEL SECURITY;
                ALTER TABLE business_capability_overrides FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON business_capability_overrides TO morrus_app
                    USING (""BusinessId""=app_current_business_id()) WITH CHECK (""BusinessId""=app_current_business_id());
                CREATE TRIGGER trg_prevent_business_change BEFORE UPDATE OF ""BusinessId""
                    ON business_capability_overrides FOR EACH ROW EXECUTE FUNCTION app_prevent_business_change();

                ALTER TABLE outlet_capability_overrides ENABLE ROW LEVEL SECURITY;
                ALTER TABLE outlet_capability_overrides FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON outlet_capability_overrides TO morrus_app
                    USING (""BusinessId""=app_current_business_id()) WITH CHECK (""BusinessId""=app_current_business_id());
                CREATE TRIGGER trg_prevent_business_change BEFORE UPDATE OF ""BusinessId""
                    ON outlet_capability_overrides FOR EACH ROW EXECUTE FUNCTION app_prevent_business_change();

                ALTER TABLE capability_change_logs ENABLE ROW LEVEL SECURITY;
                ALTER TABLE capability_change_logs FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON capability_change_logs TO morrus_app
                    USING (""BusinessId""=app_current_business_id()) WITH CHECK (""BusinessId""=app_current_business_id());
                CREATE TRIGGER trg_prevent_business_change BEFORE UPDATE OF ""BusinessId""
                    ON capability_change_logs FOR EACH ROW EXECUTE FUNCTION app_prevent_business_change();

                REVOKE ALL ON capabilities, industry_profiles, industry_profile_capabilities FROM morrus_app;
                GRANT SELECT ON capabilities, industry_profiles, industry_profile_capabilities TO morrus_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON business_capability_overrides,
                    outlet_capability_overrides TO morrus_app;
                REVOKE ALL ON capability_change_logs FROM morrus_app;
                GRANT SELECT, INSERT ON capability_change_logs TO morrus_app;
                GRANT SELECT ON capabilities, industry_profiles, industry_profile_capabilities,
                    business_capability_overrides, outlet_capability_overrides TO morrus_system;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Capability rollback is data-preserving only. Disable enforcement instead of dropping capability data.");
        }
    }
}
