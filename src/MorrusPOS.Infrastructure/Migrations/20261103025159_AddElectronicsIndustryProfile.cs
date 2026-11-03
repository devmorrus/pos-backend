using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddElectronicsIndustryProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "industry_profiles",
                keyColumn: "Id",
                keyValue: new Guid("ca100002-0000-4000-8000-000000000002"),
                columns: new[] { "Name", "SortOrder" },
                values: new object[] { "F&B", 3 });

            migrationBuilder.UpdateData(
                table: "industry_profiles",
                keyColumn: "Id",
                keyValue: new Guid("ca100003-0000-4000-8000-000000000003"),
                columns: new[] { "Name", "SortOrder" },
                values: new object[] { "Retail Umum", 4 });

            migrationBuilder.UpdateData(
                table: "industry_profiles",
                keyColumn: "Id",
                keyValue: new Guid("ca100004-0000-4000-8000-000000000004"),
                column: "SortOrder",
                value: 5);

            migrationBuilder.UpdateData(
                table: "industry_profiles",
                keyColumn: "Id",
                keyValue: new Guid("ca100005-0000-4000-8000-000000000005"),
                column: "SortOrder",
                value: 6);

            migrationBuilder.UpdateData(
                table: "industry_profiles",
                keyColumn: "Id",
                keyValue: new Guid("ca100006-0000-4000-8000-000000000006"),
                column: "SortOrder",
                value: 7);

            migrationBuilder.InsertData(
                table: "industry_profiles",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "IsActive", "Name", "SortOrder", "UpdatedAt" },
                values: new object[] { new Guid("ca100007-0000-4000-8000-000000000007"), "electronics", new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Toko gadget, komputer, aksesori, dan perangkat elektronik.", true, "Elektronik", 2, new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "industry_profile_capabilities",
                columns: new[] { "CapabilityId", "IndustryProfileId", "IsEnabled" },
                values: new object[,]
                {
                    { new Guid("ca200000-0000-4000-8000-000000000001"), new Guid("ca100007-0000-4000-8000-000000000007"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000002"), new Guid("ca100007-0000-4000-8000-000000000007"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000003"), new Guid("ca100007-0000-4000-8000-000000000007"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000004"), new Guid("ca100007-0000-4000-8000-000000000007"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000005"), new Guid("ca100007-0000-4000-8000-000000000007"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000006"), new Guid("ca100007-0000-4000-8000-000000000007"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000007"), new Guid("ca100007-0000-4000-8000-000000000007"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000008"), new Guid("ca100007-0000-4000-8000-000000000007"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000009"), new Guid("ca100007-0000-4000-8000-000000000007"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000010"), new Guid("ca100007-0000-4000-8000-000000000007"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000011"), new Guid("ca100007-0000-4000-8000-000000000007"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000012"), new Guid("ca100007-0000-4000-8000-000000000007"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000013"), new Guid("ca100007-0000-4000-8000-000000000007"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000014"), new Guid("ca100007-0000-4000-8000-000000000007"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000015"), new Guid("ca100007-0000-4000-8000-000000000007"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000016"), new Guid("ca100007-0000-4000-8000-000000000007"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000017"), new Guid("ca100007-0000-4000-8000-000000000007"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000018"), new Guid("ca100007-0000-4000-8000-000000000007"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000019"), new Guid("ca100007-0000-4000-8000-000000000007"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000020"), new Guid("ca100007-0000-4000-8000-000000000007"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000021"), new Guid("ca100007-0000-4000-8000-000000000007"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000022"), new Guid("ca100007-0000-4000-8000-000000000007"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000023"), new Guid("ca100007-0000-4000-8000-000000000007"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000024"), new Guid("ca100007-0000-4000-8000-000000000007"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000025"), new Guid("ca100007-0000-4000-8000-000000000007"), true },
                    { new Guid("ca200000-0000-4000-8000-000000000026"), new Guid("ca100007-0000-4000-8000-000000000007"), false },
                    { new Guid("ca200000-0000-4000-8000-000000000027"), new Guid("ca100007-0000-4000-8000-000000000007"), true }
                });

            migrationBuilder.Sql("""
                WITH updated AS (
                    UPDATE businesses business
                    SET "IndustryProfileId" = 'ca100007-0000-4000-8000-000000000007'::uuid,
                        "CapabilityRevision" = business."CapabilityRevision" + 1,
                        "UpdatedAt" = now()
                    WHERE business."IndustryProfileId" = 'ca100001-0000-4000-8000-000000000001'::uuid
                      AND business."OwnerId" IS NOT NULL
                      AND EXISTS (
                          SELECT 1 FROM users owner
                          WHERE owner."Id" = business."OwnerId"
                            AND owner."BusinessId" = business."Id")
                      AND lower(trim(business."Category")) IN ('electronics', 'electronic', 'elektronik')
                    RETURNING business."Id", business."OwnerId"
                )
                INSERT INTO capability_change_logs
                    ("Id", "BusinessId", "ChangeType", "OldProfileId", "NewProfileId",
                     "Reason", "ChangedByUserId", "CreatedAt")
                SELECT md5(updated."Id"::text || ':electronics-profile-backfill')::uuid,
                       updated."Id",
                       'IndustryProfileBackfilled',
                       'ca100001-0000-4000-8000-000000000001'::uuid,
                       'ca100007-0000-4000-8000-000000000007'::uuid,
                       'Electronics profile backfill',
                       updated."OwnerId",
                       now()
                FROM updated;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Industry profile rollback may orphan tenant configuration. Roll back the application without deleting profile data.");
        }
    }
}
