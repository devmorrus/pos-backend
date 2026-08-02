using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokensAndCategoryAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "categories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "categories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "outlets",
                columns: new[] { "Id", "Address", "Code", "CreatedAt", "IsActive", "Name", "Phone", "UpdatedAt" },
                values: new object[] { new Guid("8bba5427-017e-40fb-886f-5e4c6c9a3809"), null, "OUT001", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Outlet Utama", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "Id", "Code", "Description" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "transaction.create", "Membuat Transaksi Penjualan" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "transaction.void", "Membatalkan Transaksi" },
                    { new Guid("33333333-3333-3333-3333-333333333333"), "product.manage", "Mengelola Produk dan Kategori" },
                    { new Guid("44444444-4444-4444-4444-444444444444"), "stock.manage", "Mengelola Stok (Opname & Transfer)" },
                    { new Guid("55555555-5555-5555-5555-555555555555"), "supplier.manage", "Mengelola Supplier dan PO" },
                    { new Guid("66666666-6666-6666-6666-666666666666"), "consignment.manage", "Mengelola Barang Titipan/Konsinyasi" },
                    { new Guid("77777777-7777-7777-7777-777777777777"), "report.view", "Melihat Laporan Laba Rugi & Dashboard" }
                });

            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { new Guid("a3d75ea9-ec36-4e56-9a2c-d95ea7b2671f"), "Pengelola Toko/Cabang", "KepalaCabang" },
                    { new Guid("b5e7d5a9-4674-4b5b-a81d-e59fa24285b0"), "Operator Kasir Penjualan", "Kasir" },
                    { new Guid("c457d5a9-5e74-4e2b-a71d-e59fa24285c1"), "Staf Logistik dan Persediaan", "Gudang" },
                    { new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c"), "Administrator Sistem", "Admin" },
                    { new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2"), "Staf Akuntansi dan Arus Kas", "Keuangan" },
                    { new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea"), "Pemilik Bisnis (Akses penuh semua outlet)", "Owner" }
                });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("b5e7d5a9-4674-4b5b-a81d-e59fa24285b0") },
                    { new Guid("33333333-3333-3333-3333-333333333333"), new Guid("c457d5a9-5e74-4e2b-a71d-e59fa24285c1") },
                    { new Guid("44444444-4444-4444-4444-444444444444"), new Guid("c457d5a9-5e74-4e2b-a71d-e59fa24285c1") },
                    { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("33333333-3333-3333-3333-333333333333"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("44444444-4444-4444-4444-444444444444"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("55555555-5555-5555-5555-555555555555"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("66666666-6666-6666-6666-666666666666"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("77777777-7777-7777-7777-777777777777"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("55555555-5555-5555-5555-555555555555"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") },
                    { new Guid("66666666-6666-6666-6666-666666666666"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") },
                    { new Guid("77777777-7777-7777-7777-777777777777"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") },
                    { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") },
                    { new Guid("33333333-3333-3333-3333-333333333333"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") },
                    { new Guid("44444444-4444-4444-4444-444444444444"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") },
                    { new Guid("55555555-5555-5555-5555-555555555555"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") },
                    { new Guid("66666666-6666-6666-6666-666666666666"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") },
                    { new Guid("77777777-7777-7777-7777-777777777777"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") }
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "CreatedAt", "Email", "IsActive", "LastLoginAt", "Name", "OutletId", "PasswordHash", "RoleId", "UpdatedAt" },
                values: new object[] { new Guid("a4f78de1-8a9d-4e96-857e-399fa5b5f25a"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "owner@morruspos.com", true, null, "Morrus Owner", null, "$2a$11$6ujtfLVtlAZfBxEm.Zkvc.QvceWYFEa8tDpAhsaBWianak4Lb6QDS", new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_Token",
                table: "refresh_tokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId",
                table: "refresh_tokens",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DeleteData(
                table: "outlets",
                keyColumn: "Id",
                keyValue: new Guid("8bba5427-017e-40fb-886f-5e4c6c9a3809"));

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("b5e7d5a9-4674-4b5b-a81d-e59fa24285b0") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("33333333-3333-3333-3333-333333333333"), new Guid("c457d5a9-5e74-4e2b-a71d-e59fa24285c1") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("44444444-4444-4444-4444-444444444444"), new Guid("c457d5a9-5e74-4e2b-a71d-e59fa24285c1") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("33333333-3333-3333-3333-333333333333"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("44444444-4444-4444-4444-444444444444"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("55555555-5555-5555-5555-555555555555"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("66666666-6666-6666-6666-666666666666"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("77777777-7777-7777-7777-777777777777"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("55555555-5555-5555-5555-555555555555"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("66666666-6666-6666-6666-666666666666"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("77777777-7777-7777-7777-777777777777"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("33333333-3333-3333-3333-333333333333"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("44444444-4444-4444-4444-444444444444"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("55555555-5555-5555-5555-555555555555"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("66666666-6666-6666-6666-666666666666"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("77777777-7777-7777-7777-777777777777"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") });

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("a3d75ea9-ec36-4e56-9a2c-d95ea7b2671f"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("a4f78de1-8a9d-4e96-857e-399fa5b5f25a"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("66666666-6666-6666-6666-666666666666"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("77777777-7777-7777-7777-777777777777"));

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("b5e7d5a9-4674-4b5b-a81d-e59fa24285b0"));

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("c457d5a9-5e74-4e2b-a71d-e59fa24285c1"));

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c"));

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2"));

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea"));

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "categories");
        }
    }
}
