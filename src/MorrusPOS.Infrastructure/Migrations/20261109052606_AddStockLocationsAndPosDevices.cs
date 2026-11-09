using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockLocationsAndPosDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_inventory_stock_outlets_OutletId",
                table: "inventory_stock");

            migrationBuilder.DropForeignKey(
                name: "FK_product_serials_outlets_OutletId",
                table: "product_serials");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_outlets_OutletId",
                table: "ProductBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_ledger_outlets_OutletId",
                table: "stock_ledger");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_opnames_outlets_OutletId",
                table: "stock_opnames");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_reconciliation_items_outlets_OutletId",
                table: "stock_reconciliation_items");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_reconciliation_runs_outlets_OutletId",
                table: "stock_reconciliation_runs");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_transfers_outlets_FromOutletId",
                table: "stock_transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_transfers_outlets_ToOutletId",
                table: "stock_transfers");

            migrationBuilder.DropIndex(
                name: "IX_cashier_sessions_UserId",
                table: "cashier_sessions");

            migrationBuilder.RenameColumn(
                name: "ToOutletId",
                table: "stock_transfers",
                newName: "ToStockLocationId");

            migrationBuilder.RenameColumn(
                name: "FromOutletId",
                table: "stock_transfers",
                newName: "FromStockLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_transfers_ToOutletId",
                table: "stock_transfers",
                newName: "IX_stock_transfers_ToStockLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_transfers_FromOutletId",
                table: "stock_transfers",
                newName: "IX_stock_transfers_FromStockLocationId");

            migrationBuilder.RenameColumn(
                name: "OutletId",
                table: "stock_reconciliation_runs",
                newName: "StockLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_reconciliation_runs_OutletId",
                table: "stock_reconciliation_runs",
                newName: "IX_stock_reconciliation_runs_StockLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_reconciliation_runs_BusinessId_OutletId_StartedAt",
                table: "stock_reconciliation_runs",
                newName: "IX_stock_reconciliation_runs_BusinessId_StockLocationId_Starte~");

            migrationBuilder.RenameColumn(
                name: "OutletId",
                table: "stock_reconciliation_items",
                newName: "StockLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_reconciliation_items_OutletId",
                table: "stock_reconciliation_items",
                newName: "IX_stock_reconciliation_items_StockLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_reconciliation_items_BusinessId_OutletId_ProductId_Pr~",
                table: "stock_reconciliation_items",
                newName: "IX_stock_reconciliation_items_BusinessId_StockLocationId_Produ~");

            migrationBuilder.RenameColumn(
                name: "OutletId",
                table: "stock_opnames",
                newName: "StockLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_opnames_OutletId",
                table: "stock_opnames",
                newName: "IX_stock_opnames_StockLocationId");

            migrationBuilder.RenameColumn(
                name: "OutletId",
                table: "stock_ledger",
                newName: "StockLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_ledger_ProductId_OutletId",
                table: "stock_ledger",
                newName: "IX_stock_ledger_ProductId_StockLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_ledger_OutletId",
                table: "stock_ledger",
                newName: "IX_stock_ledger_StockLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_ledger_BusinessId_OutletId_ProductId_ProductVariantId~",
                table: "stock_ledger",
                newName: "IX_stock_ledger_BusinessId_StockLocationId_ProductId_ProductVa~");

            migrationBuilder.RenameColumn(
                name: "OutletId",
                table: "ProductBatches",
                newName: "StockLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductBatches_OutletId_ProductId_ExpiryDate",
                table: "ProductBatches",
                newName: "IX_ProductBatches_StockLocationId_ProductId_ExpiryDate");

            migrationBuilder.RenameIndex(
                name: "IX_ProductBatches_BusinessId_OutletId_ProductId_ProductVariant~",
                table: "ProductBatches",
                newName: "IX_ProductBatches_BusinessId_StockLocationId_ProductId_Product~");

            migrationBuilder.RenameIndex(
                name: "IX_ProductBatches_BusinessId_OutletId_ProductId_BatchNumber",
                table: "ProductBatches",
                newName: "IX_ProductBatches_BusinessId_StockLocationId_ProductId_BatchNu~");

            migrationBuilder.RenameColumn(
                name: "OutletId",
                table: "product_serials",
                newName: "StockLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_product_serials_OutletId",
                table: "product_serials",
                newName: "IX_product_serials_StockLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_product_serials_BusinessId_OutletId_ProductId_ProductVarian~",
                table: "product_serials",
                newName: "IX_product_serials_BusinessId_StockLocationId_ProductId_Produc~");

            migrationBuilder.RenameColumn(
                name: "OutletId",
                table: "inventory_stock",
                newName: "StockLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_inventory_stock_OutletId",
                table: "inventory_stock",
                newName: "IX_inventory_stock_StockLocationId");

            migrationBuilder.RenameIndex(
                name: "IX_inventory_stock_BusinessId_ProductId_ProductVariantId_Outle~",
                table: "inventory_stock",
                newName: "IX_inventory_stock_BusinessId_ProductId_ProductVariantId_Stock~");

            migrationBuilder.RenameIndex(
                name: "IX_inventory_stock_BusinessId_ProductId_OutletId",
                table: "inventory_stock",
                newName: "IX_inventory_stock_BusinessId_ProductId_StockLocationId");

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegisterId",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StockLocationId",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DestinationStockLocationId",
                table: "purchase_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                table: "cashier_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegisterId",
                table: "cashier_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StockLocationId",
                table: "cashier_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stock_locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_locations", x => x.Id);
                    table.CheckConstraint("CK_stock_locations_OutletLink", "(\"Type\"='outlet' AND \"OutletId\" IS NOT NULL) OR (\"Type\"='warehouse' AND \"OutletId\" IS NULL)");
                    table.CheckConstraint("CK_stock_locations_Type", "\"Type\" IN ('outlet','warehouse')");
                    table.ForeignKey(
                        name: "FK_stock_locations_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_locations_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Setiap outlet selalu punya satu stock location dengan Id yang sama.
            migrationBuilder.Sql(OutletStockLocationBackfill);

            migrationBuilder.CreateTable(
                name: "pos_registers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pos_registers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pos_registers_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pos_registers_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pos_registers_stock_locations_StockLocationId",
                        column: x => x.StockLocationId,
                        principalTable: "stock_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_stock_location_access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_stock_location_access", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_stock_location_access_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_stock_location_access_stock_locations_StockLocationId",
                        column: x => x.StockLocationId,
                        principalTable: "stock_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_stock_location_access_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pos_device_pairing_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pos_device_pairing_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pos_device_pairing_codes_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pos_device_pairing_codes_pos_registers_RegisterId",
                        column: x => x.RegisterId,
                        principalTable: "pos_registers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pos_devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Platform = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CredentialHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    AppVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ActivatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pos_devices", x => x.Id);
                    table.CheckConstraint("CK_pos_devices_Platform", "\"Platform\" IN ('web','android','ios','windows','macos','linux')");
                    table.CheckConstraint("CK_pos_devices_Status", "\"Status\" IN ('active','revoked')");
                    table.ForeignKey(
                        name: "FK_pos_devices_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pos_devices_pos_registers_RegisterId",
                        column: x => x.RegisterId,
                        principalTable: "pos_registers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_DeviceId",
                table: "transactions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_RegisterId",
                table: "transactions",
                column: "RegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_StockLocationId",
                table: "transactions",
                column: "StockLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_DestinationStockLocationId",
                table: "purchase_orders",
                column: "DestinationStockLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_cashier_sessions_DeviceId",
                table: "cashier_sessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_cashier_sessions_RegisterId",
                table: "cashier_sessions",
                column: "RegisterId",
                unique: true,
                filter: "\"Status\"='open' AND \"RegisterId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cashier_sessions_StockLocationId",
                table: "cashier_sessions",
                column: "StockLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_cashier_sessions_UserId",
                table: "cashier_sessions",
                column: "UserId",
                unique: true,
                filter: "\"Status\"='open' AND \"RegisterId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_pos_device_pairing_codes_BusinessId_CodeHash",
                table: "pos_device_pairing_codes",
                columns: new[] { "BusinessId", "CodeHash" });

            migrationBuilder.CreateIndex(
                name: "IX_pos_device_pairing_codes_BusinessId_RegisterId_ExpiresAtUtc",
                table: "pos_device_pairing_codes",
                columns: new[] { "BusinessId", "RegisterId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_pos_device_pairing_codes_ExpiresAtUtc",
                table: "pos_device_pairing_codes",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_pos_device_pairing_codes_RegisterId",
                table: "pos_device_pairing_codes",
                column: "RegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_pos_devices_BusinessId_InstallationId",
                table: "pos_devices",
                columns: new[] { "BusinessId", "InstallationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pos_devices_RegisterId",
                table: "pos_devices",
                column: "RegisterId",
                unique: true,
                filter: "\"Status\" = 'active'");

            migrationBuilder.CreateIndex(
                name: "IX_pos_registers_BusinessId_OutletId_Code",
                table: "pos_registers",
                columns: new[] { "BusinessId", "OutletId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pos_registers_BusinessId_StockLocationId",
                table: "pos_registers",
                columns: new[] { "BusinessId", "StockLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_pos_registers_OutletId",
                table: "pos_registers",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_pos_registers_StockLocationId",
                table: "pos_registers",
                column: "StockLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_locations_BusinessId_Code",
                table: "stock_locations",
                columns: new[] { "BusinessId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_locations_BusinessId_OutletId",
                table: "stock_locations",
                columns: new[] { "BusinessId", "OutletId" },
                unique: true,
                filter: "\"OutletId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_stock_locations_BusinessId_Type_IsActive_Name",
                table: "stock_locations",
                columns: new[] { "BusinessId", "Type", "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_locations_OutletId",
                table: "stock_locations",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_user_stock_location_access_BusinessId_UserId",
                table: "user_stock_location_access",
                columns: new[] { "BusinessId", "UserId" },
                unique: true,
                filter: "\"IsDefault\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_user_stock_location_access_BusinessId_UserId_StockLocationId",
                table: "user_stock_location_access",
                columns: new[] { "BusinessId", "UserId", "StockLocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_stock_location_access_StockLocationId",
                table: "user_stock_location_access",
                column: "StockLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_user_stock_location_access_UserId",
                table: "user_stock_location_access",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_cashier_sessions_pos_devices_DeviceId",
                table: "cashier_sessions",
                column: "DeviceId",
                principalTable: "pos_devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cashier_sessions_pos_registers_RegisterId",
                table: "cashier_sessions",
                column: "RegisterId",
                principalTable: "pos_registers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cashier_sessions_stock_locations_StockLocationId",
                table: "cashier_sessions",
                column: "StockLocationId",
                principalTable: "stock_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_stock_stock_locations_StockLocationId",
                table: "inventory_stock",
                column: "StockLocationId",
                principalTable: "stock_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_serials_stock_locations_StockLocationId",
                table: "product_serials",
                column: "StockLocationId",
                principalTable: "stock_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_stock_locations_StockLocationId",
                table: "ProductBatches",
                column: "StockLocationId",
                principalTable: "stock_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_orders_stock_locations_DestinationStockLocationId",
                table: "purchase_orders",
                column: "DestinationStockLocationId",
                principalTable: "stock_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_ledger_stock_locations_StockLocationId",
                table: "stock_ledger",
                column: "StockLocationId",
                principalTable: "stock_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_opnames_stock_locations_StockLocationId",
                table: "stock_opnames",
                column: "StockLocationId",
                principalTable: "stock_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_reconciliation_items_stock_locations_StockLocationId",
                table: "stock_reconciliation_items",
                column: "StockLocationId",
                principalTable: "stock_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_reconciliation_runs_stock_locations_StockLocationId",
                table: "stock_reconciliation_runs",
                column: "StockLocationId",
                principalTable: "stock_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_transfers_stock_locations_FromStockLocationId",
                table: "stock_transfers",
                column: "FromStockLocationId",
                principalTable: "stock_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_transfers_stock_locations_ToStockLocationId",
                table: "stock_transfers",
                column: "ToStockLocationId",
                principalTable: "stock_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_pos_devices_DeviceId",
                table: "transactions",
                column: "DeviceId",
                principalTable: "pos_devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_pos_registers_RegisterId",
                table: "transactions",
                column: "RegisterId",
                principalTable: "pos_registers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_stock_locations_StockLocationId",
                table: "transactions",
                column: "StockLocationId",
                principalTable: "stock_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(StockLocationProjectionFunction);
            migrationBuilder.Sql(StockLocationReconciliationFunction);
            migrationBuilder.Sql(StockLocationRlsAndGrants);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(OutletProjectionFunction);
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_reconcile_inventory_stock(uuid,boolean);");
            migrationBuilder.Sql(OutletReconciliationFunction);
            migrationBuilder.Sql("GRANT EXECUTE ON FUNCTION fn_reconcile_inventory_stock(uuid,boolean) TO morrus_app;");

            migrationBuilder.DropForeignKey(
                name: "FK_cashier_sessions_pos_devices_DeviceId",
                table: "cashier_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_cashier_sessions_pos_registers_RegisterId",
                table: "cashier_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_cashier_sessions_stock_locations_StockLocationId",
                table: "cashier_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_inventory_stock_stock_locations_StockLocationId",
                table: "inventory_stock");

            migrationBuilder.DropForeignKey(
                name: "FK_product_serials_stock_locations_StockLocationId",
                table: "product_serials");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_stock_locations_StockLocationId",
                table: "ProductBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_orders_stock_locations_DestinationStockLocationId",
                table: "purchase_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_ledger_stock_locations_StockLocationId",
                table: "stock_ledger");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_opnames_stock_locations_StockLocationId",
                table: "stock_opnames");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_reconciliation_items_stock_locations_StockLocationId",
                table: "stock_reconciliation_items");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_reconciliation_runs_stock_locations_StockLocationId",
                table: "stock_reconciliation_runs");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_transfers_stock_locations_FromStockLocationId",
                table: "stock_transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_transfers_stock_locations_ToStockLocationId",
                table: "stock_transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_pos_devices_DeviceId",
                table: "transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_pos_registers_RegisterId",
                table: "transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_stock_locations_StockLocationId",
                table: "transactions");

            migrationBuilder.DropTable(
                name: "pos_device_pairing_codes");

            migrationBuilder.DropTable(
                name: "pos_devices");

            migrationBuilder.DropTable(
                name: "user_stock_location_access");

            migrationBuilder.DropTable(
                name: "pos_registers");

            migrationBuilder.DropTable(
                name: "stock_locations");

            migrationBuilder.DropIndex(
                name: "IX_transactions_DeviceId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_RegisterId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_StockLocationId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_purchase_orders_DestinationStockLocationId",
                table: "purchase_orders");

            migrationBuilder.DropIndex(
                name: "IX_cashier_sessions_DeviceId",
                table: "cashier_sessions");

            migrationBuilder.DropIndex(
                name: "IX_cashier_sessions_RegisterId",
                table: "cashier_sessions");

            migrationBuilder.DropIndex(
                name: "IX_cashier_sessions_StockLocationId",
                table: "cashier_sessions");

            migrationBuilder.DropIndex(
                name: "IX_cashier_sessions_UserId",
                table: "cashier_sessions");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "RegisterId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "StockLocationId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "DestinationStockLocationId",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "cashier_sessions");

            migrationBuilder.DropColumn(
                name: "RegisterId",
                table: "cashier_sessions");

            migrationBuilder.DropColumn(
                name: "StockLocationId",
                table: "cashier_sessions");

            migrationBuilder.RenameColumn(
                name: "ToStockLocationId",
                table: "stock_transfers",
                newName: "ToOutletId");

            migrationBuilder.RenameColumn(
                name: "FromStockLocationId",
                table: "stock_transfers",
                newName: "FromOutletId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_transfers_ToStockLocationId",
                table: "stock_transfers",
                newName: "IX_stock_transfers_ToOutletId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_transfers_FromStockLocationId",
                table: "stock_transfers",
                newName: "IX_stock_transfers_FromOutletId");

            migrationBuilder.RenameColumn(
                name: "StockLocationId",
                table: "stock_reconciliation_runs",
                newName: "OutletId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_reconciliation_runs_StockLocationId",
                table: "stock_reconciliation_runs",
                newName: "IX_stock_reconciliation_runs_OutletId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_reconciliation_runs_BusinessId_StockLocationId_Starte~",
                table: "stock_reconciliation_runs",
                newName: "IX_stock_reconciliation_runs_BusinessId_OutletId_StartedAt");

            migrationBuilder.RenameColumn(
                name: "StockLocationId",
                table: "stock_reconciliation_items",
                newName: "OutletId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_reconciliation_items_StockLocationId",
                table: "stock_reconciliation_items",
                newName: "IX_stock_reconciliation_items_OutletId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_reconciliation_items_BusinessId_StockLocationId_Produ~",
                table: "stock_reconciliation_items",
                newName: "IX_stock_reconciliation_items_BusinessId_OutletId_ProductId_Pr~");

            migrationBuilder.RenameColumn(
                name: "StockLocationId",
                table: "stock_opnames",
                newName: "OutletId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_opnames_StockLocationId",
                table: "stock_opnames",
                newName: "IX_stock_opnames_OutletId");

            migrationBuilder.RenameColumn(
                name: "StockLocationId",
                table: "stock_ledger",
                newName: "OutletId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_ledger_StockLocationId",
                table: "stock_ledger",
                newName: "IX_stock_ledger_OutletId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_ledger_ProductId_StockLocationId",
                table: "stock_ledger",
                newName: "IX_stock_ledger_ProductId_OutletId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_ledger_BusinessId_StockLocationId_ProductId_ProductVa~",
                table: "stock_ledger",
                newName: "IX_stock_ledger_BusinessId_OutletId_ProductId_ProductVariantId~");

            migrationBuilder.RenameColumn(
                name: "StockLocationId",
                table: "ProductBatches",
                newName: "OutletId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductBatches_StockLocationId_ProductId_ExpiryDate",
                table: "ProductBatches",
                newName: "IX_ProductBatches_OutletId_ProductId_ExpiryDate");

            migrationBuilder.RenameIndex(
                name: "IX_ProductBatches_BusinessId_StockLocationId_ProductId_Product~",
                table: "ProductBatches",
                newName: "IX_ProductBatches_BusinessId_OutletId_ProductId_ProductVariant~");

            migrationBuilder.RenameIndex(
                name: "IX_ProductBatches_BusinessId_StockLocationId_ProductId_BatchNu~",
                table: "ProductBatches",
                newName: "IX_ProductBatches_BusinessId_OutletId_ProductId_BatchNumber");

            migrationBuilder.RenameColumn(
                name: "StockLocationId",
                table: "product_serials",
                newName: "OutletId");

            migrationBuilder.RenameIndex(
                name: "IX_product_serials_StockLocationId",
                table: "product_serials",
                newName: "IX_product_serials_OutletId");

            migrationBuilder.RenameIndex(
                name: "IX_product_serials_BusinessId_StockLocationId_ProductId_Produc~",
                table: "product_serials",
                newName: "IX_product_serials_BusinessId_OutletId_ProductId_ProductVarian~");

            migrationBuilder.RenameColumn(
                name: "StockLocationId",
                table: "inventory_stock",
                newName: "OutletId");

            migrationBuilder.RenameIndex(
                name: "IX_inventory_stock_StockLocationId",
                table: "inventory_stock",
                newName: "IX_inventory_stock_OutletId");

            migrationBuilder.RenameIndex(
                name: "IX_inventory_stock_BusinessId_ProductId_StockLocationId",
                table: "inventory_stock",
                newName: "IX_inventory_stock_BusinessId_ProductId_OutletId");

            migrationBuilder.RenameIndex(
                name: "IX_inventory_stock_BusinessId_ProductId_ProductVariantId_Stock~",
                table: "inventory_stock",
                newName: "IX_inventory_stock_BusinessId_ProductId_ProductVariantId_Outle~");

            migrationBuilder.CreateIndex(
                name: "IX_cashier_sessions_UserId",
                table: "cashier_sessions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_stock_outlets_OutletId",
                table: "inventory_stock",
                column: "OutletId",
                principalTable: "outlets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_serials_outlets_OutletId",
                table: "product_serials",
                column: "OutletId",
                principalTable: "outlets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_outlets_OutletId",
                table: "ProductBatches",
                column: "OutletId",
                principalTable: "outlets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_ledger_outlets_OutletId",
                table: "stock_ledger",
                column: "OutletId",
                principalTable: "outlets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_opnames_outlets_OutletId",
                table: "stock_opnames",
                column: "OutletId",
                principalTable: "outlets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_reconciliation_items_outlets_OutletId",
                table: "stock_reconciliation_items",
                column: "OutletId",
                principalTable: "outlets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_reconciliation_runs_outlets_OutletId",
                table: "stock_reconciliation_runs",
                column: "OutletId",
                principalTable: "outlets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_transfers_outlets_FromOutletId",
                table: "stock_transfers",
                column: "FromOutletId",
                principalTable: "outlets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_transfers_outlets_ToOutletId",
                table: "stock_transfers",
                column: "ToOutletId",
                principalTable: "outlets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        private const string OutletStockLocationBackfill = """
            INSERT INTO stock_locations
                ("Id","BusinessId","Type","Code","Name","OutletId","Address","Phone","IsActive","CreatedAt","UpdatedAt")
            SELECT o."Id", o."BusinessId", 'outlet', o."Code", o."Name", o."Id",
                   o."Address", o."Phone", o."IsActive", now(), now()
            FROM outlets o
            WHERE NOT EXISTS (SELECT 1 FROM stock_locations l WHERE l."Id" = o."Id");
            """;

        private const string StockLocationProjectionFunction = """
            CREATE OR REPLACE FUNCTION fn_update_inventory_stock()
            RETURNS trigger
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path=pg_catalog,public
            AS $fn$
            BEGIN
                PERFORM pg_advisory_xact_lock_shared(hashtextextended(NEW."BusinessId"::text || ':' || NEW."StockLocationId"::text,0));
                IF NOT EXISTS (
                    SELECT 1 FROM public.products p
                    WHERE p."BusinessId"=NEW."BusinessId" AND p."Id"=NEW."ProductId"
                      AND p."TrackingMode"<>'none')
                THEN
                    RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: produk tidak valid atau tidak memiliki stok';
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM public.stock_locations l
                    WHERE l."BusinessId"=NEW."BusinessId" AND l."Id"=NEW."StockLocationId" AND l."IsActive")
                THEN
                    RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: lokasi stok tidak valid';
                END IF;

                IF NEW."ProductVariantId" IS NOT NULL THEN
                    UPDATE public.inventory_stock
                    SET "QtyOnHand"="QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now()
                    WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId"
                      AND "ProductVariantId"=NEW."ProductVariantId" AND "StockLocationId"=NEW."StockLocationId"
                      AND "QtyOnHand"+NEW."QtyChange">=0;
                    IF FOUND THEN RETURN NEW; END IF;

                    IF EXISTS (
                        SELECT 1 FROM public.inventory_stock
                        WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId"
                          AND "ProductVariantId"=NEW."ProductVariantId" AND "StockLocationId"=NEW."StockLocationId")
                    THEN
                        RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: stok tidak mencukupi';
                    END IF;
                ELSE
                    UPDATE public.inventory_stock
                    SET "QtyOnHand"="QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now()
                    WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId"
                      AND "ProductVariantId" IS NULL AND "StockLocationId"=NEW."StockLocationId"
                      AND "QtyOnHand"+NEW."QtyChange">=0;
                    IF FOUND THEN RETURN NEW; END IF;

                    IF EXISTS (
                        SELECT 1 FROM public.inventory_stock
                        WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId"
                          AND "ProductVariantId" IS NULL AND "StockLocationId"=NEW."StockLocationId")
                    THEN
                        RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: stok tidak mencukupi';
                    END IF;
                END IF;

                IF NEW."QtyChange"<0 THEN
                    RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: stok tidak mencukupi';
                END IF;

                IF NEW."ProductVariantId" IS NOT NULL THEN
                    INSERT INTO public.inventory_stock
                        ("Id","BusinessId","ProductId","ProductVariantId","StockLocationId","QtyOnHand","MinStockAlert","UpdatedAt")
                    VALUES
                        (gen_random_uuid(),NEW."BusinessId",NEW."ProductId",NEW."ProductVariantId",NEW."StockLocationId",NEW."QtyChange",0,now())
                    ON CONFLICT ("BusinessId","ProductId","ProductVariantId","StockLocationId")
                        WHERE "ProductVariantId" IS NOT NULL
                    DO UPDATE SET "QtyOnHand"=public.inventory_stock."QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now();
                ELSE
                    INSERT INTO public.inventory_stock
                        ("Id","BusinessId","ProductId","StockLocationId","QtyOnHand","MinStockAlert","UpdatedAt")
                    VALUES
                        (gen_random_uuid(),NEW."BusinessId",NEW."ProductId",NEW."StockLocationId",NEW."QtyChange",0,now())
                    ON CONFLICT ("BusinessId","ProductId","StockLocationId") WHERE "ProductVariantId" IS NULL
                    DO UPDATE SET "QtyOnHand"=public.inventory_stock."QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now();
                END IF;
                RETURN NEW;
            END
            $fn$;
            """;

        private const string StockLocationReconciliationFunction = """
            DROP FUNCTION IF EXISTS fn_reconcile_inventory_stock(uuid,boolean);

            CREATE OR REPLACE FUNCTION fn_reconcile_inventory_stock(p_stock_location_id uuid, p_apply boolean)
            RETURNS TABLE(
                "ProductId" uuid,
                "ProductVariantId" uuid,
                "TrackingMode" text,
                "LedgerQty" numeric,
                "StoredQty" numeric,
                "Difference" numeric,
                "BatchQty" numeric,
                "AvailableSerialCount" integer,
                "IssueType" text,
                "Repairable" boolean,
                "Action" text)
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path=pg_catalog,public
            AS $fn$
            DECLARE tenant_id uuid;
            BEGIN
                tenant_id := nullif(current_setting('app.business_id',true),'')::uuid;
                IF tenant_id IS NULL OR NOT EXISTS (
                    SELECT 1 FROM public.stock_locations l
                    WHERE l."BusinessId"=tenant_id AND l."Id"=p_stock_location_id)
                THEN
                    RAISE EXCEPTION 'STOCK_RECONCILIATION_SCOPE_INVALID: lokasi stok tidak berada dalam business aktif';
                END IF;
                IF p_apply THEN
                    PERFORM pg_advisory_xact_lock(hashtextextended(tenant_id::text || ':' || p_stock_location_id::text,0));
                END IF;

                DROP TABLE IF EXISTS pg_temp.stock_reconcile_work;
                CREATE TEMP TABLE stock_reconcile_work ON COMMIT DROP AS
                WITH ledger AS (
                    SELECT l."ProductId",l."ProductVariantId",sum(l."QtyChange") AS qty
                    FROM public.stock_ledger l
                    WHERE l."BusinessId"=tenant_id AND l."StockLocationId"=p_stock_location_id
                    GROUP BY l."ProductId",l."ProductVariantId"
                ),
                balances AS (
                    SELECT s."ProductId",s."ProductVariantId",s."QtyOnHand" AS qty,true AS balance_exists
                    FROM public.inventory_stock s
                    WHERE s."BusinessId"=tenant_id AND s."StockLocationId"=p_stock_location_id
                ),
                keys AS (
                    SELECT coalesce(l."ProductId",b."ProductId") AS product_id,
                           coalesce(l."ProductVariantId",b."ProductVariantId") AS variant_id,
                           coalesce(l.qty,0) AS ledger_qty,coalesce(b.qty,0) AS stored_qty,
                           coalesce(b.balance_exists,false) AS balance_exists
                    FROM ledger l FULL JOIN balances b
                      ON l."ProductId"=b."ProductId"
                     AND l."ProductVariantId" IS NOT DISTINCT FROM b."ProductVariantId"
                ),
                base AS (
                    SELECT k.*,p."TrackingMode" AS tracking_mode,
                           coalesce(bt.qty,0) AS batch_qty,
                           coalesce(st.qty,0)::int AS serial_qty
                    FROM keys k
                    JOIN public.products p ON p."BusinessId"=tenant_id AND p."Id"=k.product_id
                    LEFT JOIN LATERAL (
                        SELECT sum(pb."QtyRemaining") AS qty FROM public."ProductBatches" pb
                        WHERE pb."BusinessId"=tenant_id AND pb."StockLocationId"=p_stock_location_id
                          AND pb."ProductId"=k.product_id
                          AND pb."ProductVariantId" IS NOT DISTINCT FROM k.variant_id) bt ON true
                    LEFT JOIN LATERAL (
                        SELECT count(*) AS qty FROM public.product_serials ps
                        WHERE ps."BusinessId"=tenant_id AND ps."StockLocationId"=p_stock_location_id
                          AND ps."ProductId"=k.product_id
                          AND ps."ProductVariantId" IS NOT DISTINCT FROM k.variant_id
                          AND ps."Status"='available') st ON true
                ),
                classified AS (
                    SELECT b.*,
                        CASE
                          WHEN ledger_qty<0 THEN 'negative_ledger'
                          WHEN tracking_mode='none' AND (ledger_qty<>0 OR stored_qty<>0) THEN 'untracked_product_nonzero'
                          WHEN tracking_mode='batch' AND batch_qty<>ledger_qty THEN 'batch_projection_mismatch'
                          WHEN tracking_mode='serial' AND serial_qty<>ledger_qty THEN 'serial_projection_mismatch'
                          WHEN ledger_qty<>stored_qty THEN 'balance_mismatch'
                          ELSE NULL
                        END AS issue_type
                    FROM base b)
                SELECT product_id AS "ProductId",variant_id AS "ProductVariantId",
                       tracking_mode::text AS "TrackingMode",ledger_qty AS "LedgerQty",
                       stored_qty AS "StoredQty",ledger_qty-stored_qty AS "Difference",
                       batch_qty AS "BatchQty",serial_qty AS "AvailableSerialCount",
                       issue_type::text AS "IssueType",issue_type='balance_mismatch' AS "Repairable",
                       balance_exists AS "BalanceExists"
                FROM classified WHERE issue_type IS NOT NULL;

                IF p_apply AND EXISTS (SELECT 1 FROM stock_reconcile_work w WHERE NOT w."Repairable") THEN
                    RAISE EXCEPTION 'STOCK_RECONCILIATION_BLOCKED: terdapat proyeksi yang tidak aman diperbaiki otomatis';
                END IF;
                IF p_apply THEN
                    UPDATE public.inventory_stock s
                    SET "QtyOnHand"=w."LedgerQty","UpdatedAt"=now()
                    FROM stock_reconcile_work w
                    WHERE w."Repairable" AND w."BalanceExists"
                      AND s."BusinessId"=tenant_id AND s."StockLocationId"=p_stock_location_id
                      AND s."ProductId"=w."ProductId"
                      AND s."ProductVariantId" IS NOT DISTINCT FROM w."ProductVariantId";
                    INSERT INTO public.inventory_stock
                        ("Id","BusinessId","ProductId","ProductVariantId","StockLocationId","QtyOnHand","MinStockAlert","UpdatedAt")
                    SELECT gen_random_uuid(),tenant_id,w."ProductId",w."ProductVariantId",p_stock_location_id,w."LedgerQty",0,now()
                    FROM stock_reconcile_work w
                    WHERE w."Repairable" AND NOT w."BalanceExists" AND w."LedgerQty"<>0;
                END IF;

                RETURN QUERY SELECT w."ProductId",w."ProductVariantId",w."TrackingMode",
                    w."LedgerQty",w."StoredQty",w."Difference",w."BatchQty",w."AvailableSerialCount",
                    w."IssueType",w."Repairable",
                    CASE WHEN NOT p_apply THEN 'none'
                         WHEN NOT w."Repairable" THEN 'blocked'
                         WHEN w."BalanceExists" THEN 'updated_balance'
                         ELSE 'inserted_balance' END::text
                FROM stock_reconcile_work w;
            END
            $fn$;

            REVOKE ALL ON FUNCTION fn_reconcile_inventory_stock(uuid,boolean) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION fn_reconcile_inventory_stock(uuid,boolean) TO morrus_app;
            """;

        private const string StockLocationRlsAndGrants = """
            DO $rls$
            DECLARE t text;
            BEGIN
                FOREACH t IN ARRAY ARRAY[
                    'stock_locations','user_stock_location_access','pos_registers',
                    'pos_devices','pos_device_pairing_codes']
                LOOP
                    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', t);
                    EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', t);
                    EXECUTE format('CREATE POLICY tenant_isolation ON %I TO morrus_app USING ("BusinessId"=app_current_business_id()) WITH CHECK ("BusinessId"=app_current_business_id())', t);
                    EXECUTE format('DROP TRIGGER IF EXISTS trg_prevent_business_change ON %I', t);
                    EXECUTE format('CREATE TRIGGER trg_prevent_business_change BEFORE UPDATE OF "BusinessId" ON %I FOR EACH ROW EXECUTE FUNCTION app_prevent_business_change()', t);
                END LOOP;
            END
            $rls$;

            REVOKE ALL ON stock_locations, user_stock_location_access, pos_registers,
                pos_devices, pos_device_pairing_codes FROM morrus_app, morrus_system;
            GRANT SELECT, INSERT, UPDATE, DELETE ON stock_locations, user_stock_location_access,
                pos_registers, pos_devices, pos_device_pairing_codes TO morrus_app;
            GRANT SELECT, INSERT ON stock_locations TO morrus_system;
            """;

        private const string OutletProjectionFunction = """
            CREATE OR REPLACE FUNCTION fn_update_inventory_stock()
            RETURNS trigger
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path=pg_catalog,public
            AS $fn$
            BEGIN
                PERFORM pg_advisory_xact_lock_shared(hashtextextended(NEW."BusinessId"::text || ':' || NEW."OutletId"::text,0));
                IF NOT EXISTS (
                    SELECT 1 FROM public.products p
                    WHERE p."BusinessId"=NEW."BusinessId" AND p."Id"=NEW."ProductId"
                      AND p."TrackingMode"<>'none')
                THEN
                    RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: produk tidak valid atau tidak memiliki stok';
                END IF;

                IF NEW."ProductVariantId" IS NOT NULL THEN
                    UPDATE public.inventory_stock
                    SET "QtyOnHand"="QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now()
                    WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId"
                      AND "ProductVariantId"=NEW."ProductVariantId" AND "OutletId"=NEW."OutletId"
                      AND "QtyOnHand"+NEW."QtyChange">=0;
                    IF FOUND THEN RETURN NEW; END IF;

                    IF EXISTS (
                        SELECT 1 FROM public.inventory_stock
                        WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId"
                          AND "ProductVariantId"=NEW."ProductVariantId" AND "OutletId"=NEW."OutletId")
                    THEN
                        RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: stok tidak mencukupi';
                    END IF;
                ELSE
                    UPDATE public.inventory_stock
                    SET "QtyOnHand"="QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now()
                    WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId"
                      AND "ProductVariantId" IS NULL AND "OutletId"=NEW."OutletId"
                      AND "QtyOnHand"+NEW."QtyChange">=0;
                    IF FOUND THEN RETURN NEW; END IF;

                    IF EXISTS (
                        SELECT 1 FROM public.inventory_stock
                        WHERE "BusinessId"=NEW."BusinessId" AND "ProductId"=NEW."ProductId"
                          AND "ProductVariantId" IS NULL AND "OutletId"=NEW."OutletId")
                    THEN
                        RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: stok tidak mencukupi';
                    END IF;
                END IF;

                IF NEW."QtyChange"<0 THEN
                    RAISE EXCEPTION 'STOCK_MOVEMENT_INVALID: stok tidak mencukupi';
                END IF;

                IF NEW."ProductVariantId" IS NOT NULL THEN
                    INSERT INTO public.inventory_stock
                        ("Id","BusinessId","ProductId","ProductVariantId","OutletId","QtyOnHand","MinStockAlert","UpdatedAt")
                    VALUES
                        (gen_random_uuid(),NEW."BusinessId",NEW."ProductId",NEW."ProductVariantId",NEW."OutletId",NEW."QtyChange",0,now())
                    ON CONFLICT ("BusinessId","ProductId","ProductVariantId","OutletId")
                        WHERE "ProductVariantId" IS NOT NULL
                    DO UPDATE SET "QtyOnHand"=public.inventory_stock."QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now();
                ELSE
                    INSERT INTO public.inventory_stock
                        ("Id","BusinessId","ProductId","OutletId","QtyOnHand","MinStockAlert","UpdatedAt")
                    VALUES
                        (gen_random_uuid(),NEW."BusinessId",NEW."ProductId",NEW."OutletId",NEW."QtyChange",0,now())
                    ON CONFLICT ("BusinessId","ProductId","OutletId") WHERE "ProductVariantId" IS NULL
                    DO UPDATE SET "QtyOnHand"=public.inventory_stock."QtyOnHand"+NEW."QtyChange", "UpdatedAt"=now();
                END IF;
                RETURN NEW;
            END
            $fn$;
            """;

        private const string OutletReconciliationFunction = """
            CREATE OR REPLACE FUNCTION fn_reconcile_inventory_stock(p_outlet_id uuid, p_apply boolean)
            RETURNS TABLE(
                "ProductId" uuid,
                "ProductVariantId" uuid,
                "TrackingMode" text,
                "LedgerQty" numeric,
                "StoredQty" numeric,
                "Difference" numeric,
                "BatchQty" numeric,
                "AvailableSerialCount" integer,
                "IssueType" text,
                "Repairable" boolean,
                "Action" text)
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path=pg_catalog,public
            AS $fn$
            DECLARE tenant_id uuid;
            BEGIN
                tenant_id := nullif(current_setting('app.business_id',true),'')::uuid;
                IF tenant_id IS NULL OR NOT EXISTS (
                    SELECT 1 FROM public.outlets o WHERE o."BusinessId"=tenant_id AND o."Id"=p_outlet_id)
                THEN
                    RAISE EXCEPTION 'STOCK_RECONCILIATION_SCOPE_INVALID: outlet tidak berada dalam business aktif';
                END IF;
                IF p_apply THEN
                    PERFORM pg_advisory_xact_lock(hashtextextended(tenant_id::text || ':' || p_outlet_id::text,0));
                END IF;

                DROP TABLE IF EXISTS pg_temp.stock_reconcile_work;
                CREATE TEMP TABLE stock_reconcile_work ON COMMIT DROP AS
                WITH ledger AS (
                    SELECT l."ProductId",l."ProductVariantId",sum(l."QtyChange") AS qty
                    FROM public.stock_ledger l
                    WHERE l."BusinessId"=tenant_id AND l."OutletId"=p_outlet_id
                    GROUP BY l."ProductId",l."ProductVariantId"
                ),
                balances AS (
                    SELECT s."ProductId",s."ProductVariantId",s."QtyOnHand" AS qty,true AS balance_exists
                    FROM public.inventory_stock s
                    WHERE s."BusinessId"=tenant_id AND s."OutletId"=p_outlet_id
                ),
                keys AS (
                    SELECT coalesce(l."ProductId",b."ProductId") AS product_id,
                           coalesce(l."ProductVariantId",b."ProductVariantId") AS variant_id,
                           coalesce(l.qty,0) AS ledger_qty,coalesce(b.qty,0) AS stored_qty,
                           coalesce(b.balance_exists,false) AS balance_exists
                    FROM ledger l FULL JOIN balances b
                      ON l."ProductId"=b."ProductId"
                     AND l."ProductVariantId" IS NOT DISTINCT FROM b."ProductVariantId"
                ),
                base AS (
                    SELECT k.*,p."TrackingMode" AS tracking_mode,
                           coalesce(bt.qty,0) AS batch_qty,
                           coalesce(st.qty,0)::int AS serial_qty
                    FROM keys k
                    JOIN public.products p ON p."BusinessId"=tenant_id AND p."Id"=k.product_id
                    LEFT JOIN LATERAL (
                        SELECT sum(pb."QtyRemaining") AS qty FROM public."ProductBatches" pb
                        WHERE pb."BusinessId"=tenant_id AND pb."OutletId"=p_outlet_id
                          AND pb."ProductId"=k.product_id
                          AND pb."ProductVariantId" IS NOT DISTINCT FROM k.variant_id) bt ON true
                    LEFT JOIN LATERAL (
                        SELECT count(*) AS qty FROM public.product_serials ps
                        WHERE ps."BusinessId"=tenant_id AND ps."OutletId"=p_outlet_id
                          AND ps."ProductId"=k.product_id
                          AND ps."ProductVariantId" IS NOT DISTINCT FROM k.variant_id
                          AND ps."Status"='available') st ON true
                ),
                classified AS (
                    SELECT b.*,
                        CASE
                          WHEN ledger_qty<0 THEN 'negative_ledger'
                          WHEN tracking_mode='none' AND (ledger_qty<>0 OR stored_qty<>0) THEN 'untracked_product_nonzero'
                          WHEN tracking_mode='batch' AND batch_qty<>ledger_qty THEN 'batch_projection_mismatch'
                          WHEN tracking_mode='serial' AND serial_qty<>ledger_qty THEN 'serial_projection_mismatch'
                          WHEN ledger_qty<>stored_qty THEN 'balance_mismatch'
                          ELSE NULL
                        END AS issue_type
                    FROM base b)
                SELECT product_id AS "ProductId",variant_id AS "ProductVariantId",
                       tracking_mode::text AS "TrackingMode",ledger_qty AS "LedgerQty",
                       stored_qty AS "StoredQty",ledger_qty-stored_qty AS "Difference",
                       batch_qty AS "BatchQty",serial_qty AS "AvailableSerialCount",
                       issue_type::text AS "IssueType",issue_type='balance_mismatch' AS "Repairable",
                       balance_exists AS "BalanceExists"
                FROM classified WHERE issue_type IS NOT NULL;

                IF p_apply AND EXISTS (SELECT 1 FROM stock_reconcile_work w WHERE NOT w."Repairable") THEN
                    RAISE EXCEPTION 'STOCK_RECONCILIATION_BLOCKED: terdapat proyeksi yang tidak aman diperbaiki otomatis';
                END IF;
                IF p_apply THEN
                    UPDATE public.inventory_stock s
                    SET "QtyOnHand"=w."LedgerQty","UpdatedAt"=now()
                    FROM stock_reconcile_work w
                    WHERE w."Repairable" AND w."BalanceExists"
                      AND s."BusinessId"=tenant_id AND s."OutletId"=p_outlet_id
                      AND s."ProductId"=w."ProductId"
                      AND s."ProductVariantId" IS NOT DISTINCT FROM w."ProductVariantId";
                    INSERT INTO public.inventory_stock
                        ("Id","BusinessId","ProductId","ProductVariantId","OutletId","QtyOnHand","MinStockAlert","UpdatedAt")
                    SELECT gen_random_uuid(),tenant_id,w."ProductId",w."ProductVariantId",p_outlet_id,w."LedgerQty",0,now()
                    FROM stock_reconcile_work w
                    WHERE w."Repairable" AND NOT w."BalanceExists" AND w."LedgerQty"<>0;
                END IF;

                RETURN QUERY SELECT w."ProductId",w."ProductVariantId",w."TrackingMode",
                    w."LedgerQty",w."StoredQty",w."Difference",w."BatchQty",w."AvailableSerialCount",
                    w."IssueType",w."Repairable",
                    CASE WHEN NOT p_apply THEN 'none'
                         WHEN NOT w."Repairable" THEN 'blocked'
                         WHEN w."BalanceExists" THEN 'updated_balance'
                         ELSE 'inserted_balance' END::text
                FROM stock_reconcile_work w;
            END
            $fn$;
            """;
    }
}
