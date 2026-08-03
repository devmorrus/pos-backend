using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKepalaCabangRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("a3d75ea9-ec36-4e56-9a2c-d95ea7b2671f") },
                    { new Guid("33333333-3333-3333-3333-333333333333"), new Guid("a3d75ea9-ec36-4e56-9a2c-d95ea7b2671f") },
                    { new Guid("44444444-4444-4444-4444-444444444444"), new Guid("a3d75ea9-ec36-4e56-9a2c-d95ea7b2671f") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("a3d75ea9-ec36-4e56-9a2c-d95ea7b2671f") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("33333333-3333-3333-3333-333333333333"), new Guid("a3d75ea9-ec36-4e56-9a2c-d95ea7b2671f") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("44444444-4444-4444-4444-444444444444"), new Guid("a3d75ea9-ec36-4e56-9a2c-d95ea7b2671f") });
        }
    }
}
