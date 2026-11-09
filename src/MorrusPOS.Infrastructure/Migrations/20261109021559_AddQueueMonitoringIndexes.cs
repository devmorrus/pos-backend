using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQueueMonitoringIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_gobiz_order_inboxes_BusinessId",
                table: "gobiz_order_inboxes");

            migrationBuilder.CreateIndex(
                name: "IX_gobiz_order_inboxes_BusinessId_OutletId_Status_ReceivedAtUtc",
                table: "gobiz_order_inboxes",
                columns: new[] { "BusinessId", "OutletId", "Status", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_gobiz_order_inboxes_Status_ReceivedAtUtc",
                table: "gobiz_order_inboxes",
                columns: new[] { "Status", "ReceivedAtUtc" });

            migrationBuilder.Sql("""
                DO $preflight$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM gobiz_order_inboxes
                        WHERE "Status" NOT IN ('received','processing','processed','failed','dead_letter'))
                    THEN
                        RAISE EXCEPTION 'AddQueueMonitoringIndexes gagal: status GoBiz inbox tidak dikenal';
                    END IF;
                END
                $preflight$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_gobiz_order_inboxes_Status",
                table: "gobiz_order_inboxes",
                sql: "\"Status\" IN ('received','processing','processed','failed','dead_letter')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_gobiz_order_inboxes_BusinessId_OutletId_Status_ReceivedAtUtc",
                table: "gobiz_order_inboxes");

            migrationBuilder.DropIndex(
                name: "IX_gobiz_order_inboxes_Status_ReceivedAtUtc",
                table: "gobiz_order_inboxes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_gobiz_order_inboxes_Status",
                table: "gobiz_order_inboxes");

            migrationBuilder.CreateIndex(
                name: "IX_gobiz_order_inboxes_BusinessId",
                table: "gobiz_order_inboxes",
                column: "BusinessId");
        }
    }
}
