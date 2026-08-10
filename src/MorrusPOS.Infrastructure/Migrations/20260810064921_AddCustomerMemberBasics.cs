using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerMemberBasics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChannelOrderReference",
                table: "transactions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerNameSnapshot",
                table: "transactions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerPhoneSnapshot",
                table: "transactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerType",
                table: "transactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalCustomerName",
                table: "transactions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalCustomerPhone",
                table: "transactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalCustomerReference",
                table: "transactions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoyaltyReference",
                table: "transactions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BirthDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsMember = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MemberStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PointsBalance = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    LifetimeSpend = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    LastTransactionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customers_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_customers_outlets_CreatedOutletId",
                        column: x => x.CreatedOutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "Id", "Code", "Description" },
                values: new object[,]
                {
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "customer.manage", "Mengelola master customer dan member dasar" },
                    { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), "customer.view", "Melihat dan lookup customer untuk transaksi kasir" }
                });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), new Guid("a3d75ea9-ec36-4e56-9a2c-d95ea7b2671f") },
                    { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), new Guid("a3d75ea9-ec36-4e56-9a2c-d95ea7b2671f") },
                    { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), new Guid("b5e7d5a9-4674-4b5b-a81d-e59fa24285b0") },
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") },
                    { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") },
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") },
                    { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_CustomerId",
                table: "transactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_ExternalCustomerReference",
                table: "transactions",
                column: "ExternalCustomerReference");

            migrationBuilder.CreateIndex(
                name: "IX_customers_BusinessId_Phone",
                table: "customers",
                columns: new[] { "BusinessId", "Phone" },
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_customers_CreatedOutletId",
                table: "customers",
                column: "CreatedOutletId");

            migrationBuilder.CreateIndex(
                name: "IX_customers_CustomerCode",
                table: "customers",
                column: "CustomerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_LastTransactionAt",
                table: "customers",
                column: "LastTransactionAt");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_customers_CustomerId",
                table: "transactions",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transactions_customers_CustomerId",
                table: "transactions");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropIndex(
                name: "IX_transactions_CustomerId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_ExternalCustomerReference",
                table: "transactions");

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), new Guid("a3d75ea9-ec36-4e56-9a2c-d95ea7b2671f") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), new Guid("a3d75ea9-ec36-4e56-9a2c-d95ea7b2671f") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), new Guid("b5e7d5a9-4674-4b5b-a81d-e59fa24285b0") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"));

            migrationBuilder.DropColumn(
                name: "ChannelOrderReference",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "CustomerNameSnapshot",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "CustomerPhoneSnapshot",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "CustomerType",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "ExternalCustomerName",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "ExternalCustomerPhone",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "ExternalCustomerReference",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "LoyaltyReference",
                table: "transactions");
        }
    }
}
