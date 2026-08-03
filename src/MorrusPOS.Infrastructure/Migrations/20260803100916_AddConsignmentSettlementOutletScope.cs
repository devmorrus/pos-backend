using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConsignmentSettlementOutletScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OutletId",
                table: "consignment_settlements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_consignment_settlements_OutletId",
                table: "consignment_settlements",
                column: "OutletId");

            migrationBuilder.AddForeignKey(
                name: "FK_consignment_settlements_outlets_OutletId",
                table: "consignment_settlements",
                column: "OutletId",
                principalTable: "outlets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_consignment_settlements_outlets_OutletId",
                table: "consignment_settlements");

            migrationBuilder.DropIndex(
                name: "IX_consignment_settlements_OutletId",
                table: "consignment_settlements");

            migrationBuilder.DropColumn(
                name: "OutletId",
                table: "consignment_settlements");
        }
    }
}
