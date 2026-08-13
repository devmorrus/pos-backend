using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultBusinessForDevOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "businesses",
                columns: new[] { "Id", "Category", "CreatedAt", "Name", "OwnerId", "Phone", "SelectedPackage", "SubscriptionEndDate", "SubscriptionStatus", "TrialEndDate", "TrialStartDate", "UpdatedAt" },
                values: new object[] { new Guid("11111111-2222-3333-4444-555555555555"), "Retail", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Morrus Demo Business", new Guid("a4f78de1-8a9d-4e96-857e-399fa5b5f25a"), null, "Development", null, "Active", new DateTime(2026, 1, 31, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "outlets",
                keyColumn: "Id",
                keyValue: new Guid("8bba5427-017e-40fb-886f-5e4c6c9a3809"),
                column: "BusinessId",
                value: new Guid("11111111-2222-3333-4444-555555555555"));

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("a4f78de1-8a9d-4e96-857e-399fa5b5f25a"),
                column: "BusinessId",
                value: new Guid("11111111-2222-3333-4444-555555555555"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "businesses",
                keyColumn: "Id",
                keyValue: new Guid("11111111-2222-3333-4444-555555555555"));

            migrationBuilder.UpdateData(
                table: "outlets",
                keyColumn: "Id",
                keyValue: new Guid("8bba5427-017e-40fb-886f-5e4c6c9a3809"),
                column: "BusinessId",
                value: null);

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("a4f78de1-8a9d-4e96-857e-399fa5b5f25a"),
                column: "BusinessId",
                value: null);
        }
    }
}
