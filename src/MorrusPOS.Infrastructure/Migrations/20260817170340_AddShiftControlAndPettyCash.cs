using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftControlAndPettyCash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CashierSessionId",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "petty_cash_expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProcessedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_petty_cash_expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_petty_cash_expenses_cashier_sessions_CashierSessionId",
                        column: x => x.CashierSessionId,
                        principalTable: "cashier_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_petty_cash_expenses_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_petty_cash_expenses_users_ProcessedBy",
                        column: x => x.ProcessedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payments_CashierSessionId",
                table: "payments",
                column: "CashierSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_expenses_CashierSessionId",
                table: "petty_cash_expenses",
                column: "CashierSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_expenses_OutletId",
                table: "petty_cash_expenses",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_expenses_ProcessedBy",
                table: "petty_cash_expenses",
                column: "ProcessedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_payments_cashier_sessions_CashierSessionId",
                table: "payments",
                column: "CashierSessionId",
                principalTable: "cashier_sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payments_cashier_sessions_CashierSessionId",
                table: "payments");

            migrationBuilder.DropTable(
                name: "petty_cash_expenses");

            migrationBuilder.DropIndex(
                name: "IX_payments_CashierSessionId",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "CashierSessionId",
                table: "payments");
        }
    }
}
