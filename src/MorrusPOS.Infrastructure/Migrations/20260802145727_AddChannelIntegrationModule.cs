using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelIntegrationModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "channel_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MerchantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApiKey = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channel_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_channel_accounts_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "integration_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestPayload = table.Column<string>(type: "text", nullable: true),
                    ResponsePayload = table.Column<string>(type: "text", nullable: true),
                    StatusCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "channel_settlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SettlementDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channel_settlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_channel_settlements_channel_accounts_ChannelAccountId",
                        column: x => x.ChannelAccountId,
                        principalTable: "channel_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_channel_settlements_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "channel_settlement_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelSettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channel_settlement_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_channel_settlement_items_channel_settlements_ChannelSettlem~",
                        column: x => x.ChannelSettlementId,
                        principalTable: "channel_settlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_channel_settlement_items_transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_channel_accounts_OutletId",
                table: "channel_accounts",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_channel_settlement_items_ChannelSettlementId",
                table: "channel_settlement_items",
                column: "ChannelSettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_channel_settlement_items_TransactionId",
                table: "channel_settlement_items",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_channel_settlements_ChannelAccountId",
                table: "channel_settlements",
                column: "ChannelAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_channel_settlements_CreatedBy",
                table: "channel_settlements",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_channel_settlements_SettlementNumber",
                table: "channel_settlements",
                column: "SettlementNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channel_settlement_items");

            migrationBuilder.DropTable(
                name: "integration_logs");

            migrationBuilder.DropTable(
                name: "channel_settlements");

            migrationBuilder.DropTable(
                name: "channel_accounts");
        }
    }
}
