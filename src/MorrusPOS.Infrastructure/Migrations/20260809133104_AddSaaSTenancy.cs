using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaaSTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "suppliers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "outlets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "businesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SubscriptionStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TrialStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrialEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubscriptionEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SelectedPackage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_businesses", x => x.Id);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_users_BusinessId",
                table: "users",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_BusinessId",
                table: "suppliers",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_products_BusinessId",
                table: "products",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_outlets_BusinessId",
                table: "outlets",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_categories_BusinessId",
                table: "categories",
                column: "BusinessId");

            migrationBuilder.AddForeignKey(
                name: "FK_categories_businesses_BusinessId",
                table: "categories",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_outlets_businesses_BusinessId",
                table: "outlets",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_businesses_BusinessId",
                table: "products",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_suppliers_businesses_BusinessId",
                table: "suppliers",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_users_businesses_BusinessId",
                table: "users",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_categories_businesses_BusinessId",
                table: "categories");

            migrationBuilder.DropForeignKey(
                name: "FK_outlets_businesses_BusinessId",
                table: "outlets");

            migrationBuilder.DropForeignKey(
                name: "FK_products_businesses_BusinessId",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_suppliers_businesses_BusinessId",
                table: "suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_users_businesses_BusinessId",
                table: "users");

            migrationBuilder.DropTable(
                name: "businesses");

            migrationBuilder.DropIndex(
                name: "IX_users_BusinessId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_suppliers_BusinessId",
                table: "suppliers");

            migrationBuilder.DropIndex(
                name: "IX_products_BusinessId",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_outlets_BusinessId",
                table: "outlets");

            migrationBuilder.DropIndex(
                name: "IX_categories_BusinessId",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "products");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "outlets");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "categories");
        }
    }
}
