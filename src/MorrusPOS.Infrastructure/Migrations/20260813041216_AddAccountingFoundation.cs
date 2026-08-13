using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chart_of_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AccountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsCashBank = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chart_of_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chart_of_accounts_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chart_of_accounts_chart_of_accounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalTable: "chart_of_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_chart_of_accounts_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "account_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrxDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrxNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrxEntity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChartOfAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    DebitAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_transactions_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_account_transactions_chart_of_accounts_ChartOfAccountId",
                        column: x => x.ChartOfAccountId,
                        principalTable: "chart_of_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_account_transactions_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "cash_flows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrxNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TrxDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrxType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TrxEntity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FromChartOfAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToChartOfAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AttachmentUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_flows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cash_flows_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cash_flows_chart_of_accounts_FromChartOfAccountId",
                        column: x => x.FromChartOfAccountId,
                        principalTable: "chart_of_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_flows_chart_of_accounts_ToChartOfAccountId",
                        column: x => x.ToChartOfAccountId,
                        principalTable: "chart_of_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_flows_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cash_flows_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "Id", "Code", "Description" },
                values: new object[,]
                {
                    { new Guid("12121212-1212-1212-1212-121212121212"), "report.cashflow.view", "Melihat laporan arus kas" },
                    { new Guid("13131313-1313-1313-1313-131313131313"), "report.profitloss_accounting.view", "Melihat laporan laba rugi akuntansi" },
                    { new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), "account.manage", "Mengelola master chart of accounts" },
                    { new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), "cashflow.create", "Membuat transaksi pemasukan dan pengeluaran toko" },
                    { new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"), "cashflow.view", "Melihat histori transaksi cash flow manual" }
                });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("12121212-1212-1212-1212-121212121212"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("13131313-1313-1313-1313-131313131313"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") },
                    { new Guid("12121212-1212-1212-1212-121212121212"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") },
                    { new Guid("13131313-1313-1313-1313-131313131313"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") },
                    { new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") },
                    { new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") },
                    { new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") },
                    { new Guid("12121212-1212-1212-1212-121212121212"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") },
                    { new Guid("13131313-1313-1313-1313-131313131313"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") },
                    { new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") },
                    { new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") },
                    { new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_transactions_BusinessId_ReferenceType_ReferenceId",
                table: "account_transactions",
                columns: new[] { "BusinessId", "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_account_transactions_BusinessId_TrxDate",
                table: "account_transactions",
                columns: new[] { "BusinessId", "TrxDate" });

            migrationBuilder.CreateIndex(
                name: "IX_account_transactions_BusinessId_TrxEntity",
                table: "account_transactions",
                columns: new[] { "BusinessId", "TrxEntity" });

            migrationBuilder.CreateIndex(
                name: "IX_account_transactions_ChartOfAccountId",
                table: "account_transactions",
                column: "ChartOfAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_account_transactions_OutletId",
                table: "account_transactions",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_flows_BusinessId_TrxDate",
                table: "cash_flows",
                columns: new[] { "BusinessId", "TrxDate" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_flows_BusinessId_TrxNumber",
                table: "cash_flows",
                columns: new[] { "BusinessId", "TrxNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_flows_BusinessId_TrxType",
                table: "cash_flows",
                columns: new[] { "BusinessId", "TrxType" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_flows_CreatedBy",
                table: "cash_flows",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_cash_flows_FromChartOfAccountId",
                table: "cash_flows",
                column: "FromChartOfAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_flows_OutletId",
                table: "cash_flows",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_flows_ToChartOfAccountId",
                table: "cash_flows",
                column: "ToChartOfAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_chart_of_accounts_BusinessId_AccountCode",
                table: "chart_of_accounts",
                columns: new[] { "BusinessId", "AccountCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chart_of_accounts_BusinessId_AccountType",
                table: "chart_of_accounts",
                columns: new[] { "BusinessId", "AccountType" });

            migrationBuilder.CreateIndex(
                name: "IX_chart_of_accounts_BusinessId_IsCashBank",
                table: "chart_of_accounts",
                columns: new[] { "BusinessId", "IsCashBank" });

            migrationBuilder.CreateIndex(
                name: "IX_chart_of_accounts_OutletId",
                table: "chart_of_accounts",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_chart_of_accounts_ParentAccountId",
                table: "chart_of_accounts",
                column: "ParentAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_transactions");

            migrationBuilder.DropTable(
                name: "cash_flows");

            migrationBuilder.DropTable(
                name: "chart_of_accounts");

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("12121212-1212-1212-1212-121212121212"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("13131313-1313-1313-1313-131313131313"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"), new Guid("d54f590a-6e54-4f05-8461-8ff62dfd8d4c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("12121212-1212-1212-1212-121212121212"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("13131313-1313-1313-1313-131313131313"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"), new Guid("d667d5a9-6e74-4e2b-b81d-e59fa24285d2") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("12121212-1212-1212-1212-121212121212"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("13131313-1313-1313-1313-131313131313"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"), new Guid("e1a7b077-44a3-4b63-95e0-59a8501170ea") });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("12121212-1212-1212-1212-121212121212"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("13131313-1313-1313-1313-131313131313"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        }
    }
}
