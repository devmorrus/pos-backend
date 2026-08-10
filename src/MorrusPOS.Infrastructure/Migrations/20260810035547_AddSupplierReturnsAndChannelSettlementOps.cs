using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierReturnsAndChannelSettlementOps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_supplier_return_items_SupplierReturnId",
                table: "supplier_return_items");

            migrationBuilder.DropIndex(
                name: "IX_channel_settlement_items_ChannelSettlementId",
                table: "channel_settlement_items");

            migrationBuilder.DropIndex(
                name: "IX_channel_accounts_OutletId",
                table: "channel_accounts");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "supplier_returns",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnNumber",
                table: "supplier_returns",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "supplier_returns",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEndDate",
                table: "channel_settlements",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStartDate",
                table: "channel_settlements",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultCommissionRate",
                table: "channel_accounts",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "channel_accounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "Id", "Code", "Description" },
                values: new object[,]
                {
                    { new Guid("88888888-8888-8888-8888-888888888888"), "supplier_return.manage", "Mengelola retur supplier" },
                    { new Guid("99999999-9999-9999-9999-999999999999"), "channel_settlement.manage", "Mengelola channel account dan settlement channel" }
                });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("88888888-8888-8888-8888-888888888888"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("99999999-9999-9999-9999-999999999999"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("88888888-8888-8888-8888-888888888888"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") },
                    { new Guid("99999999-9999-9999-9999-999999999999"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") },
                    { new Guid("88888888-8888-8888-8888-888888888888"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") },
                    { new Guid("99999999-9999-9999-9999-999999999999"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_returns_ReturnNumber",
                table: "supplier_returns",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_return_items_SupplierReturnId_ProductId",
                table: "supplier_return_items",
                columns: new[] { "SupplierReturnId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_channel_settlement_items_ChannelSettlementId_TransactionId",
                table: "channel_settlement_items",
                columns: new[] { "ChannelSettlementId", "TransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_channel_accounts_OutletId_ChannelName_Name",
                table: "channel_accounts",
                columns: new[] { "OutletId", "ChannelName", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_supplier_returns_ReturnNumber",
                table: "supplier_returns");

            migrationBuilder.DropIndex(
                name: "IX_supplier_return_items_SupplierReturnId_ProductId",
                table: "supplier_return_items");

            migrationBuilder.DropIndex(
                name: "IX_channel_settlement_items_ChannelSettlementId_TransactionId",
                table: "channel_settlement_items");

            migrationBuilder.DropIndex(
                name: "IX_channel_accounts_OutletId_ChannelName_Name",
                table: "channel_accounts");

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("88888888-8888-8888-8888-888888888888"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("99999999-9999-9999-9999-999999999999"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("88888888-8888-8888-8888-888888888888"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("99999999-9999-9999-9999-999999999999"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("88888888-8888-8888-8888-888888888888"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("99999999-9999-9999-9999-999999999999"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("88888888-8888-8888-8888-888888888888"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"));

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "supplier_returns");

            migrationBuilder.DropColumn(
                name: "ReturnNumber",
                table: "supplier_returns");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "supplier_returns");

            migrationBuilder.DropColumn(
                name: "PeriodEndDate",
                table: "channel_settlements");

            migrationBuilder.DropColumn(
                name: "PeriodStartDate",
                table: "channel_settlements");

            migrationBuilder.DropColumn(
                name: "DefaultCommissionRate",
                table: "channel_accounts");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "channel_accounts");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_return_items_SupplierReturnId",
                table: "supplier_return_items",
                column: "SupplierReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_channel_settlement_items_ChannelSettlementId",
                table: "channel_settlement_items",
                column: "ChannelSettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_channel_accounts_OutletId",
                table: "channel_accounts",
                column: "OutletId");
        }
    }
}
