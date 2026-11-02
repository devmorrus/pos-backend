using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outlets_BusinessId",
                table: "outlets");

            migrationBuilder.DropIndex(
                name: "IX_outlets_Code",
                table: "outlets");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "businesses",
                type: "character varying(180)",
                maxLength: 180,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE businesses
                SET ""Slug"" = CASE
                    WHEN ""Id""='11111111-2222-3333-4444-555555555555' THEN 'morrus-demo'
                    ELSE coalesce(
                        nullif(trim(both '-' from regexp_replace(lower(""Name""),'[^a-z0-9]+','-','g')),''),
                        'business'
                    ) || '-' || left(replace(""Id""::text,'-',''),8)
                END;
                ALTER TABLE businesses ALTER COLUMN ""Slug"" SET NOT NULL;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_outlets_BusinessId_Code",
                table: "outlets",
                columns: new[] { "BusinessId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_businesses_Slug",
                table: "businesses",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outlets_BusinessId_Code",
                table: "outlets");

            migrationBuilder.DropIndex(
                name: "IX_businesses_Slug",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "businesses");

            migrationBuilder.CreateIndex(
                name: "IX_outlets_BusinessId",
                table: "outlets",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_outlets_Code",
                table: "outlets",
                column: "Code",
                unique: true);
        }
    }
}
