using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteTenantIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_account_transactions_businesses_BusinessId",
                table: "account_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_cash_flows_businesses_BusinessId",
                table: "cash_flows");

            migrationBuilder.DropForeignKey(
                name: "FK_chart_of_accounts_businesses_BusinessId",
                table: "chart_of_accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_customers_businesses_BusinessId",
                table: "customers");

            migrationBuilder.DropForeignKey(
                name: "FK_ModifierGroups_businesses_BusinessId",
                table: "ModifierGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductAttributes_businesses_BusinessId",
                table: "ProductAttributes");

            migrationBuilder.DropIndex(
                name: "IX_transactions_TransactionNumber",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_stock_transfers_TransferNumber",
                table: "stock_transfers");

            migrationBuilder.DropIndex(
                name: "IX_purchase_orders_PoNumber",
                table: "purchase_orders");

            migrationBuilder.DropIndex(
                name: "IX_products_Barcode",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_BusinessId",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_Sku",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_inventory_stock_ProductId_OutletId",
                table: "inventory_stock");

            migrationBuilder.DropIndex(
                name: "IX_inventory_stock_ProductId_ProductVariantId_OutletId",
                table: "inventory_stock");

            migrationBuilder.DropIndex(
                name: "IX_customers_CustomerCode",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_consignments_ConsignmentNumber",
                table: "consignments");

            migrationBuilder.DropIndex(
                name: "IX_consignment_settlements_SettlementNumber",
                table: "consignment_settlements");

            migrationBuilder.DropIndex(
                name: "IX_consignment_returns_ReturnNumber",
                table: "consignment_returns");

            migrationBuilder.DropIndex(
                name: "IX_channel_settlements_SettlementNumber",
                table: "channel_settlements");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "vouchers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "voucher_redemptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "transaction_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "tax_rules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "supplier_returns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "supplier_return_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "supplier_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "supplier_debts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "stock_transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "stock_transfer_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "stock_opnames",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "stock_opname_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "stock_ledger",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "service_charge_rules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "returns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "refresh_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "ReceivingNotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "ReceivingNoteItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "purchase_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "purchase_order_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "promo_campaigns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "promo_campaign_targets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "ProductVariants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "ProductRecipes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "ProductBatches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "ProductAttributeValues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "petty_cash_expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "ModifierOptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "ModifierGroupProduct",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "inventory_stock",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "integration_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "gobiz_product_mappings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "gobiz_order_inboxes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "consignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "consignment_settlements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "consignment_sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "consignment_returns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "consignment_return_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "consignment_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "channel_stock_policies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "channel_settlements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "channel_settlement_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "channel_accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "cashier_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "audit_logs",
                type: "uuid",
                nullable: true);


            // STAGE 2 — deterministic parent backfill. The five explicit UUID
            // mappings are legacy development seed rows identified by preflight.
            migrationBuilder.Sql(@"
                UPDATE outlets SET ""BusinessId""='11111111-2222-3333-4444-555555555555'
                 WHERE ""BusinessId"" IS NULL AND ""Id"" IN
                 ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1','bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb2');
                UPDATE users SET ""BusinessId""='11111111-2222-3333-4444-555555555555'
                 WHERE ""BusinessId"" IS NULL AND ""Id""='22222222-2222-2222-2222-222222222222';
                UPDATE products SET ""BusinessId""='11111111-2222-3333-4444-555555555555'
                 WHERE ""BusinessId"" IS NULL AND ""Id"" IN
                 ('6bec8d2f-b9aa-4f3b-a2ca-b1cdcd7ec232','d0000000-0000-4000-8000-000000000001',
                  'd0000000-0000-4000-8000-000000000002','d0000000-0000-4000-8000-000000000003');
                UPDATE categories SET ""BusinessId""='11111111-2222-3333-4444-555555555555'
                 WHERE ""BusinessId"" IS NULL AND ""Id"" IN
                 ('c0000000-0000-4000-8000-000000000001','c0000000-0000-4000-8000-000000000002','c0000000-0000-4000-8000-000000000003');
                UPDATE suppliers SET ""BusinessId""='11111111-2222-3333-4444-555555555555'
                 WHERE ""BusinessId"" IS NULL AND ""Id"" IN
                 ('f0000000-0000-4000-8000-000000000001','f0000000-0000-4000-8000-000000000002');
                UPDATE integration_logs SET ""BusinessId""='11111111-2222-3333-4444-555555555555'
                 WHERE ""BusinessId"" IS NULL AND ""Id""='ef40c049-a33f-452c-8213-cfa0bc1c32df';

                -- Direct outlet scope
                UPDATE transactions t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE cashier_sessions t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE inventory_stock t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE stock_ledger t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE petty_cash_expenses t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE tax_rules t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE service_charge_rules t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE promo_campaigns t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE vouchers t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE stock_opnames t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE stock_transfers t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""FromOutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE channel_accounts t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE channel_stock_policies t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE purchase_orders t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE consignments t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE consignment_settlements t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE consignment_returns t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE audit_logs t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE gobiz_product_mappings t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE gobiz_integrations t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE gobiz_oauth_states t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE gobiz_direct_integrations t SET ""BusinessId""=o.""BusinessId"" FROM outlets o WHERE t.""OutletId""=o.""Id"" AND t.""BusinessId"" IS NULL;

                -- Other direct roots
                UPDATE supplier_debts t SET ""BusinessId""=p.""BusinessId"" FROM suppliers p WHERE t.""SupplierId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE supplier_payments t SET ""BusinessId""=p.""BusinessId"" FROM suppliers p WHERE t.""SupplierId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE consignment_sales t SET ""BusinessId""=p.""BusinessId"" FROM suppliers p WHERE t.""SupplierId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE ""ProductVariants"" t SET ""BusinessId""=p.""BusinessId"" FROM products p WHERE t.""ProductId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE ""ProductRecipes"" t SET ""BusinessId""=p.""BusinessId"" FROM products p WHERE t.""ProductId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE ""ProductBatches"" t SET ""BusinessId""=p.""BusinessId"" FROM products p WHERE t.""ProductId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE ""ModifierGroupProduct"" t SET ""BusinessId""=p.""BusinessId"" FROM products p WHERE t.""ProductsId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE refresh_tokens t SET ""BusinessId""=u.""BusinessId"" FROM users u WHERE t.""UserId""=u.""Id"" AND t.""BusinessId"" IS NULL;

                -- Children derive only from the already backfilled parent
                UPDATE transaction_items t SET ""BusinessId""=p.""BusinessId"" FROM transactions p WHERE t.""TransactionId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE payments t SET ""BusinessId""=p.""BusinessId"" FROM transactions p WHERE t.""TransactionId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE returns t SET ""BusinessId""=p.""BusinessId"" FROM transactions p WHERE t.""TransactionId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE voucher_redemptions t SET ""BusinessId""=p.""BusinessId"" FROM transactions p WHERE t.""TransactionId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE stock_opname_items t SET ""BusinessId""=p.""BusinessId"" FROM stock_opnames p WHERE t.""StockOpnameId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE stock_transfer_items t SET ""BusinessId""=p.""BusinessId"" FROM stock_transfers p WHERE t.""StockTransferId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE purchase_order_items t SET ""BusinessId""=p.""BusinessId"" FROM purchase_orders p WHERE t.""PurchaseOrderId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE ""ReceivingNotes"" t SET ""BusinessId""=p.""BusinessId"" FROM purchase_orders p WHERE t.""PurchaseOrderId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE ""ReceivingNoteItems"" t SET ""BusinessId""=p.""BusinessId"" FROM ""ReceivingNotes"" p WHERE t.""ReceivingNoteId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE supplier_returns t SET ""BusinessId""=p.""BusinessId"" FROM purchase_orders p WHERE t.""PurchaseOrderId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE supplier_return_items t SET ""BusinessId""=p.""BusinessId"" FROM supplier_returns p WHERE t.""SupplierReturnId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE consignment_items t SET ""BusinessId""=p.""BusinessId"" FROM consignments p WHERE t.""ConsignmentId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE consignment_return_items t SET ""BusinessId""=p.""BusinessId"" FROM consignment_returns p WHERE t.""ConsignmentReturnId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE channel_settlements t SET ""BusinessId""=p.""BusinessId"" FROM channel_accounts p WHERE t.""ChannelAccountId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE channel_settlement_items t SET ""BusinessId""=p.""BusinessId"" FROM channel_settlements p WHERE t.""ChannelSettlementId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE ""ProductAttributeValues"" t SET ""BusinessId""=p.""BusinessId"" FROM ""ProductVariants"" p WHERE t.""ProductVariantId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE ""ModifierOptions"" t SET ""BusinessId""=p.""BusinessId"" FROM ""ModifierGroups"" p WHERE t.""ModifierGroupId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE promo_campaign_targets t SET ""BusinessId""=p.""BusinessId"" FROM promo_campaigns p WHERE t.""PromoCampaignId""=p.""Id"" AND t.""BusinessId"" IS NULL;
                UPDATE gobiz_order_inboxes t SET ""BusinessId""=p.""BusinessId"" FROM outlets p WHERE t.""OutletId""=p.""Id"" AND t.""BusinessId"" IS NULL;
            ");

            // Any unknown orphan aborts the transaction; deployment must provide
            // an explicit business mapping instead of silently assigning it.
            migrationBuilder.Sql(@"
                DO $guard$
                DECLARE tbl text; remaining bigint;
                BEGIN
                    FOREACH tbl IN ARRAY ARRAY[
                        'outlets','users','products','categories','suppliers','customers',
                        'chart_of_accounts','cash_flows','account_transactions','ProductAttributes','ModifierGroups',
                        'transactions','transaction_items','payments','returns','cashier_sessions','petty_cash_expenses',
                        'inventory_stock','stock_ledger','stock_opnames','stock_opname_items','stock_transfers','stock_transfer_items','channel_stock_policies',
                        'ProductVariants','ProductRecipes','ProductBatches','ProductAttributeValues','ModifierOptions','ModifierGroupProduct','audit_logs',
                        'purchase_orders','purchase_order_items','ReceivingNotes','ReceivingNoteItems','supplier_debts','supplier_payments','supplier_returns','supplier_return_items',
                        'consignments','consignment_items','consignment_sales','consignment_settlements','consignment_returns','consignment_return_items',
                        'channel_accounts','channel_settlements','channel_settlement_items','integration_logs',
                        'gobiz_integrations','gobiz_oauth_states','gobiz_client_configs','gobiz_direct_integrations','gobiz_product_mappings','gobiz_order_inboxes',
                        'tax_rules','service_charge_rules','promo_campaigns','promo_campaign_targets','vouchers','voucher_redemptions','refresh_tokens'
                    ] LOOP
                        EXECUTE format('SELECT count(*) FROM %I WHERE ""BusinessId"" IS NULL', tbl) INTO remaining;
                        IF remaining > 0 THEN
                            RAISE EXCEPTION 'BusinessId backfill incomplete: table % has % NULL rows', tbl, remaining;
                        END IF;
                    END LOOP;
                END
                $guard$;
            ");

            // STAGE 3 — enforce mandatory tenant key after successful validation.
            migrationBuilder.Sql(@"
                DO $notnull$
                DECLARE tbl text;
                BEGIN
                    FOREACH tbl IN ARRAY ARRAY[
                        'outlets','users','products','categories','suppliers','customers',
                        'chart_of_accounts','cash_flows','account_transactions','ProductAttributes','ModifierGroups',
                        'transactions','transaction_items','payments','returns','cashier_sessions','petty_cash_expenses',
                        'inventory_stock','stock_ledger','stock_opnames','stock_opname_items','stock_transfers','stock_transfer_items','channel_stock_policies',
                        'ProductVariants','ProductRecipes','ProductBatches','ProductAttributeValues','ModifierOptions','ModifierGroupProduct','audit_logs',
                        'purchase_orders','purchase_order_items','ReceivingNotes','ReceivingNoteItems','supplier_debts','supplier_payments','supplier_returns','supplier_return_items',
                        'consignments','consignment_items','consignment_sales','consignment_settlements','consignment_returns','consignment_return_items',
                        'channel_accounts','channel_settlements','channel_settlement_items','integration_logs',
                        'gobiz_integrations','gobiz_oauth_states','gobiz_client_configs','gobiz_direct_integrations','gobiz_product_mappings','gobiz_order_inboxes',
                        'tax_rules','service_charge_rules','promo_campaigns','promo_campaign_targets','vouchers','voucher_redemptions','refresh_tokens'
                    ] LOOP
                        EXECUTE format('ALTER TABLE %I ALTER COLUMN ""BusinessId"" SET NOT NULL', tbl);
                    END LOOP;
                END
                $notnull$;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_BusinessId",
                table: "vouchers",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_redemptions_BusinessId",
                table: "voucher_redemptions",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_BusinessId_TransactionNumber",
                table: "transactions",
                columns: new[] { "BusinessId", "TransactionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transaction_items_BusinessId",
                table: "transaction_items",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_tax_rules_BusinessId",
                table: "tax_rules",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_returns_BusinessId",
                table: "supplier_returns",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_return_items_BusinessId",
                table: "supplier_return_items",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payments_BusinessId",
                table: "supplier_payments",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_debts_BusinessId",
                table: "supplier_debts",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_BusinessId_TransferNumber",
                table: "stock_transfers",
                columns: new[] { "BusinessId", "TransferNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_items_BusinessId",
                table: "stock_transfer_items",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_opnames_BusinessId",
                table: "stock_opnames",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_opname_items_BusinessId",
                table: "stock_opname_items",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_BusinessId",
                table: "stock_ledger",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_service_charge_rules_BusinessId",
                table: "service_charge_rules",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_returns_BusinessId",
                table: "returns",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_BusinessId",
                table: "refresh_tokens",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingNotes_BusinessId",
                table: "ReceivingNotes",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingNoteItems_BusinessId",
                table: "ReceivingNoteItems",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_BusinessId_PoNumber",
                table: "purchase_orders",
                columns: new[] { "BusinessId", "PoNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_items_BusinessId",
                table: "purchase_order_items",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_promo_campaigns_BusinessId",
                table: "promo_campaigns",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_promo_campaign_targets_BusinessId",
                table: "promo_campaign_targets",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_BusinessId",
                table: "ProductVariants",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_products_BusinessId_Barcode",
                table: "products",
                columns: new[] { "BusinessId", "Barcode" },
                unique: true,
                filter: "\"Barcode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_products_BusinessId_Sku",
                table: "products",
                columns: new[] { "BusinessId", "Sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductRecipes_BusinessId",
                table: "ProductRecipes",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_BusinessId",
                table: "ProductBatches",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributeValues_BusinessId",
                table: "ProductAttributeValues",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_expenses_BusinessId",
                table: "petty_cash_expenses",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_BusinessId",
                table: "payments",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierOptions_BusinessId",
                table: "ModifierOptions",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierGroupProduct_BusinessId",
                table: "ModifierGroupProduct",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stock_BusinessId_ProductId_OutletId",
                table: "inventory_stock",
                columns: new[] { "BusinessId", "ProductId", "OutletId" },
                unique: true,
                filter: "\"ProductVariantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stock_BusinessId_ProductId_ProductVariantId_Outle~",
                table: "inventory_stock",
                columns: new[] { "BusinessId", "ProductId", "ProductVariantId", "OutletId" },
                unique: true,
                filter: "\"ProductVariantId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_stock_ProductId",
                table: "inventory_stock",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_integration_logs_BusinessId",
                table: "integration_logs",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_gobiz_product_mappings_BusinessId",
                table: "gobiz_product_mappings",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_gobiz_order_inboxes_BusinessId",
                table: "gobiz_order_inboxes",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_customers_BusinessId_CustomerCode",
                table: "customers",
                columns: new[] { "BusinessId", "CustomerCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_consignments_BusinessId_ConsignmentNumber",
                table: "consignments",
                columns: new[] { "BusinessId", "ConsignmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_consignment_settlements_BusinessId_SettlementNumber",
                table: "consignment_settlements",
                columns: new[] { "BusinessId", "SettlementNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_consignment_sales_BusinessId",
                table: "consignment_sales",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_consignment_returns_BusinessId_ReturnNumber",
                table: "consignment_returns",
                columns: new[] { "BusinessId", "ReturnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_consignment_return_items_BusinessId",
                table: "consignment_return_items",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_consignment_items_BusinessId",
                table: "consignment_items",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_channel_stock_policies_BusinessId",
                table: "channel_stock_policies",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_channel_settlements_BusinessId_SettlementNumber",
                table: "channel_settlements",
                columns: new[] { "BusinessId", "SettlementNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_channel_settlement_items_BusinessId",
                table: "channel_settlement_items",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_channel_accounts_BusinessId",
                table: "channel_accounts",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_cashier_sessions_BusinessId",
                table: "cashier_sessions",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_BusinessId",
                table: "audit_logs",
                column: "BusinessId");

            migrationBuilder.AddForeignKey(
                name: "FK_account_transactions_businesses_BusinessId",
                table: "account_transactions",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_businesses_BusinessId",
                table: "audit_logs",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cash_flows_businesses_BusinessId",
                table: "cash_flows",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cashier_sessions_businesses_BusinessId",
                table: "cashier_sessions",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_channel_accounts_businesses_BusinessId",
                table: "channel_accounts",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_channel_settlement_items_businesses_BusinessId",
                table: "channel_settlement_items",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_channel_settlements_businesses_BusinessId",
                table: "channel_settlements",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_channel_stock_policies_businesses_BusinessId",
                table: "channel_stock_policies",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_chart_of_accounts_businesses_BusinessId",
                table: "chart_of_accounts",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_consignment_items_businesses_BusinessId",
                table: "consignment_items",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_consignment_return_items_businesses_BusinessId",
                table: "consignment_return_items",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_consignment_returns_businesses_BusinessId",
                table: "consignment_returns",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_consignment_sales_businesses_BusinessId",
                table: "consignment_sales",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_consignment_settlements_businesses_BusinessId",
                table: "consignment_settlements",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_consignments_businesses_BusinessId",
                table: "consignments",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_customers_businesses_BusinessId",
                table: "customers",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_gobiz_order_inboxes_businesses_BusinessId",
                table: "gobiz_order_inboxes",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_gobiz_product_mappings_businesses_BusinessId",
                table: "gobiz_product_mappings",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_integration_logs_businesses_BusinessId",
                table: "integration_logs",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_stock_businesses_BusinessId",
                table: "inventory_stock",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ModifierGroupProduct_businesses_BusinessId",
                table: "ModifierGroupProduct",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ModifierGroups_businesses_BusinessId",
                table: "ModifierGroups",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ModifierOptions_businesses_BusinessId",
                table: "ModifierOptions",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_businesses_BusinessId",
                table: "payments",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_petty_cash_expenses_businesses_BusinessId",
                table: "petty_cash_expenses",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductAttributes_businesses_BusinessId",
                table: "ProductAttributes",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductAttributeValues_businesses_BusinessId",
                table: "ProductAttributeValues",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_businesses_BusinessId",
                table: "ProductBatches",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductRecipes_businesses_BusinessId",
                table: "ProductRecipes",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_businesses_BusinessId",
                table: "ProductVariants",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_promo_campaign_targets_businesses_BusinessId",
                table: "promo_campaign_targets",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_promo_campaigns_businesses_BusinessId",
                table: "promo_campaigns",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_order_items_businesses_BusinessId",
                table: "purchase_order_items",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_orders_businesses_BusinessId",
                table: "purchase_orders",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceivingNoteItems_businesses_BusinessId",
                table: "ReceivingNoteItems",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceivingNotes_businesses_BusinessId",
                table: "ReceivingNotes",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_refresh_tokens_businesses_BusinessId",
                table: "refresh_tokens",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_returns_businesses_BusinessId",
                table: "returns",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_service_charge_rules_businesses_BusinessId",
                table: "service_charge_rules",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_ledger_businesses_BusinessId",
                table: "stock_ledger",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_opname_items_businesses_BusinessId",
                table: "stock_opname_items",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_opnames_businesses_BusinessId",
                table: "stock_opnames",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_transfer_items_businesses_BusinessId",
                table: "stock_transfer_items",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_transfers_businesses_BusinessId",
                table: "stock_transfers",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_debts_businesses_BusinessId",
                table: "supplier_debts",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_payments_businesses_BusinessId",
                table: "supplier_payments",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_return_items_businesses_BusinessId",
                table: "supplier_return_items",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_returns_businesses_BusinessId",
                table: "supplier_returns",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tax_rules_businesses_BusinessId",
                table: "tax_rules",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_transaction_items_businesses_BusinessId",
                table: "transaction_items",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_businesses_BusinessId",
                table: "transactions",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_redemptions_businesses_BusinessId",
                table: "voucher_redemptions",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_vouchers_businesses_BusinessId",
                table: "vouchers",
                column: "BusinessId",
                principalTable: "businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // STAGE 4 — inventory trigger uses the direct tenant key and the
            // tenant-aware conflict targets created above.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION fn_update_inventory_stock()
                RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    IF NEW.""ProductVariantId"" IS NOT NULL THEN
                        INSERT INTO inventory_stock
                            (""Id"",""BusinessId"",""ProductId"",""ProductVariantId"",""OutletId"",""QtyOnHand"",""MinStockAlert"",""UpdatedAt"")
                        VALUES
                            (gen_random_uuid(),NEW.""BusinessId"",NEW.""ProductId"",NEW.""ProductVariantId"",NEW.""OutletId"",NEW.""QtyChange"",0,now())
                        ON CONFLICT (""BusinessId"",""ProductId"",""ProductVariantId"",""OutletId"")
                            WHERE ""ProductVariantId"" IS NOT NULL
                        DO UPDATE SET ""QtyOnHand""=inventory_stock.""QtyOnHand""+NEW.""QtyChange"", ""UpdatedAt""=now();
                    ELSE
                        INSERT INTO inventory_stock
                            (""Id"",""BusinessId"",""ProductId"",""OutletId"",""QtyOnHand"",""MinStockAlert"",""UpdatedAt"")
                        VALUES
                            (gen_random_uuid(),NEW.""BusinessId"",NEW.""ProductId"",NEW.""OutletId"",NEW.""QtyChange"",0,now())
                        ON CONFLICT (""BusinessId"",""ProductId"",""OutletId"")
                            WHERE ""ProductVariantId"" IS NULL
                        DO UPDATE SET ""QtyOnHand""=inventory_stock.""QtyOnHand""+NEW.""QtyChange"", ""UpdatedAt""=now();
                    END IF;
                    RETURN NEW;
                END
                $fn$;
            ");

            // STAGE 5 — database consistency: every FK between two tenant
            // tables is mirrored by a composite (BusinessId, ParentId) FK.
            // Constraints are added NOT VALID then explicitly validated to keep
            // the validation phase observable and safe for larger deployments.
            migrationBuilder.Sql(@"
                DO $role_guard$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='morrus_app') THEN
                        RAISE EXCEPTION 'Database role morrus_app must exist before tenant isolation migration';
                    END IF;
                END
                $role_guard$;

                DO $indexes$
                DECLARE r record; index_name text;
                BEGIN
                    FOR r IN
                        SELECT n.nspname, c.relname
                        FROM pg_class c
                        JOIN pg_namespace n ON n.oid=c.relnamespace
                        WHERE c.relkind='r' AND n.nspname='public'
                          AND EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid=c.oid AND a.attname='BusinessId' AND NOT a.attisdropped)
                          AND EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid=c.oid AND a.attname='Id' AND NOT a.attisdropped)
                    LOOP
                        index_name := 'UXT_' || substr(md5(r.nspname || '.' || r.relname),1,24);
                        EXECUTE format('CREATE UNIQUE INDEX IF NOT EXISTS %I ON %I.%I (""BusinessId"",""Id"")',
                                       index_name, r.nspname, r.relname);
                    END LOOP;
                END
                $indexes$;

                DO $fks$
                DECLARE r record; tenant_fk_name text;
                BEGIN
                    FOR r IN
                        SELECT cn.nspname child_schema, child.relname child_table,
                               pn.nspname parent_schema, parent.relname parent_table,
                               child_col.attname child_column, con.conname source_fk
                        FROM pg_constraint con
                        JOIN pg_class child ON child.oid=con.conrelid
                        JOIN pg_namespace cn ON cn.oid=child.relnamespace
                        JOIN pg_class parent ON parent.oid=con.confrelid
                        JOIN pg_namespace pn ON pn.oid=parent.relnamespace
                        JOIN pg_attribute child_col ON child_col.attrelid=child.oid AND child_col.attnum=con.conkey[1]
                        JOIN pg_attribute parent_col ON parent_col.attrelid=parent.oid AND parent_col.attnum=con.confkey[1]
                        WHERE con.contype='f' AND array_length(con.conkey,1)=1 AND array_length(con.confkey,1)=1
                          AND cn.nspname='public' AND pn.nspname='public' AND parent_col.attname='Id'
                          AND EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid=child.oid AND a.attname='BusinessId' AND NOT a.attisdropped)
                          AND EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid=parent.oid AND a.attname='BusinessId' AND NOT a.attisdropped)
                    LOOP
                        tenant_fk_name := 'FKT_' || substr(md5(r.child_schema || '.' || r.child_table || '.' || r.source_fk),1,24);
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname=tenant_fk_name AND connamespace='public'::regnamespace) THEN
                            EXECUTE format(
                                'ALTER TABLE %I.%I ADD CONSTRAINT %I FOREIGN KEY (""BusinessId"",%I) REFERENCES %I.%I (""BusinessId"",""Id"") NOT VALID',
                                r.child_schema,r.child_table,tenant_fk_name,r.child_column,r.parent_schema,r.parent_table);
                            EXECUTE format('ALTER TABLE %I.%I VALIDATE CONSTRAINT %I',
                                           r.child_schema,r.child_table,tenant_fk_name);
                        END IF;
                    END LOOP;
                END
                $fks$;
            ");

            // STAGE 6 — session-GUC RLS plus immutable BusinessId. The policy is
            // generated for every table that owns a BusinessId column, avoiding
            // omissions when new tenant tables are introduced by this migration.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION app_current_business_id()
                RETURNS uuid LANGUAGE sql STABLE AS $$
                    SELECT NULLIF(current_setting('app.business_id', true), '')::uuid
                $$;

                CREATE OR REPLACE FUNCTION app_prevent_business_change()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.""BusinessId"" IS DISTINCT FROM OLD.""BusinessId"" THEN
                        RAISE EXCEPTION 'BusinessId cannot be changed';
                    END IF;
                    RETURN NEW;
                END
                $$;

                DO $rls$
                DECLARE r record;
                BEGIN
                    FOR r IN
                        SELECT n.nspname, c.relname
                        FROM pg_class c
                        JOIN pg_namespace n ON n.oid=c.relnamespace
                        WHERE c.relkind='r' AND n.nspname='public'
                          AND EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid=c.oid AND a.attname='BusinessId' AND NOT a.attisdropped)
                    LOOP
                        EXECUTE format('ALTER TABLE %I.%I ENABLE ROW LEVEL SECURITY',r.nspname,r.relname);
                        EXECUTE format('ALTER TABLE %I.%I FORCE ROW LEVEL SECURITY',r.nspname,r.relname);
                        EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I.%I',r.nspname,r.relname);
                        EXECUTE format('CREATE POLICY tenant_isolation ON %I.%I TO morrus_app USING (""BusinessId""=app_current_business_id()) WITH CHECK (""BusinessId""=app_current_business_id())',r.nspname,r.relname);
                        EXECUTE format('DROP TRIGGER IF EXISTS trg_prevent_business_change ON %I.%I',r.nspname,r.relname);
                        EXECUTE format('CREATE TRIGGER trg_prevent_business_change BEFORE UPDATE OF ""BusinessId"" ON %I.%I FOR EACH ROW EXECUTE FUNCTION app_prevent_business_change()',r.nspname,r.relname);
                    END LOOP;
                END
                $rls$;

                ALTER TABLE businesses ENABLE ROW LEVEL SECURITY;
                ALTER TABLE businesses FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON businesses;
                CREATE POLICY tenant_isolation ON businesses TO morrus_app
                    USING (""Id""=app_current_business_id());
            ");

            // Fresh databases run role creation before tables exist, so final
            // least-privilege grants are also applied here after schema setup.
            migrationBuilder.Sql(@"
                GRANT USAGE ON SCHEMA public TO morrus_app, morrus_system;
                REVOKE ALL ON ALL TABLES IN SCHEMA public FROM morrus_app, morrus_system;

                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO morrus_app;
                REVOKE ALL ON businesses, roles, permissions, role_permissions,
                    refresh_tokens, ""__EFMigrationsHistory"" FROM morrus_app;
                GRANT SELECT ON businesses, roles, permissions, role_permissions TO morrus_app;
                GRANT UPDATE ON businesses TO morrus_app;

                GRANT SELECT ON businesses, outlets, users, roles, permissions,
                    role_permissions, refresh_tokens,
                    gobiz_direct_integrations, gobiz_order_inboxes,
                    gobiz_client_configs TO morrus_system;
                GRANT INSERT, UPDATE ON businesses, outlets, users, refresh_tokens TO morrus_system;
                GRANT UPDATE ON gobiz_direct_integrations, gobiz_order_inboxes TO morrus_system;

                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO morrus_app, morrus_system;
                ALTER DEFAULT PRIVILEGES IN SCHEMA public
                    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO morrus_app;
                ALTER DEFAULT PRIVILEGES IN SCHEMA public
                    GRANT USAGE, SELECT ON SEQUENCES TO morrus_app;
            ");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-preserving operational rollback. Columns and tenant keys are
            // intentionally retained; only enforcement is disabled.
            migrationBuilder.Sql(@"
                DO $down_rls$
                DECLARE r record;
                BEGIN
                    FOR r IN
                        SELECT n.nspname, c.relname
                        FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                        WHERE c.relkind='r' AND n.nspname='public'
                          AND EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid=c.oid AND a.attname='BusinessId' AND NOT a.attisdropped)
                    LOOP
                        EXECUTE format('DROP TRIGGER IF EXISTS trg_prevent_business_change ON %I.%I',r.nspname,r.relname);
                        EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I.%I',r.nspname,r.relname);
                        EXECUTE format('ALTER TABLE %I.%I NO FORCE ROW LEVEL SECURITY',r.nspname,r.relname);
                        EXECUTE format('ALTER TABLE %I.%I DISABLE ROW LEVEL SECURITY',r.nspname,r.relname);
                    END LOOP;
                END
                $down_rls$;

                DO $down_fk$
                DECLARE r record;
                BEGIN
                    FOR r IN
                        SELECT n.nspname, c.relname, con.conname
                        FROM pg_constraint con
                        JOIN pg_class c ON c.oid=con.conrelid
                        JOIN pg_namespace n ON n.oid=c.relnamespace
                        WHERE n.nspname='public' AND con.conname LIKE 'FKT_%'
                    LOOP
                        EXECUTE format('ALTER TABLE %I.%I DROP CONSTRAINT %I',r.nspname,r.relname,r.conname);
                    END LOOP;
                END
                $down_fk$;

                DROP POLICY IF EXISTS tenant_isolation ON businesses;
                ALTER TABLE businesses NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE businesses DISABLE ROW LEVEL SECURITY;
            ");
        }
    }
}
