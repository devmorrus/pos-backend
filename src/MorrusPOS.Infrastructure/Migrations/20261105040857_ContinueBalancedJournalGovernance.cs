using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ContinueBalancedJournalGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SettlementTreatment",
                table: "supplier_returns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "payable");

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCostSnapshot",
                table: "stock_opname_items",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CogsAmount",
                table: "returns",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ConsignmentCogsAmount",
                table: "returns",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                table: "returns",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RevenueAmount",
                table: "returns",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceChargeAmount",
                table: "returns",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "returns",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "accounting_account_mappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ChartOfAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_account_mappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounting_account_mappings_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_account_mappings_chart_of_accounts_ChartOfAccoun~",
                        column: x => x.ChartOfAccountId,
                        principalTable: "chart_of_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_account_mappings_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_supplier_returns_SettlementTreatment",
                table: "supplier_returns",
                sql: "\"SettlementTreatment\" IN ('payable','receivable','cash_refund')");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_account_mappings_BusinessId_OutletId_Role",
                table: "accounting_account_mappings",
                columns: new[] { "BusinessId", "OutletId", "Role" },
                unique: true,
                filter: "\"OutletId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_account_mappings_BusinessId_Role",
                table: "accounting_account_mappings",
                columns: new[] { "BusinessId", "Role" },
                unique: true,
                filter: "\"OutletId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_account_mappings_ChartOfAccountId",
                table: "accounting_account_mappings",
                column: "ChartOfAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_account_mappings_OutletId",
                table: "accounting_account_mappings",
                column: "OutletId");

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UXT_accounting_account_mappings_BusinessId_Id"
                    ON accounting_account_mappings ("BusinessId","Id");
                ALTER TABLE accounting_account_mappings
                    ADD CONSTRAINT "FKT_accounting_account_mappings_outlet"
                    FOREIGN KEY ("BusinessId","OutletId") REFERENCES outlets ("BusinessId","Id") NOT VALID,
                    ADD CONSTRAINT "FKT_accounting_account_mappings_account"
                    FOREIGN KEY ("BusinessId","ChartOfAccountId") REFERENCES chart_of_accounts ("BusinessId","Id") NOT VALID;
                ALTER TABLE accounting_account_mappings VALIDATE CONSTRAINT "FKT_accounting_account_mappings_outlet";
                ALTER TABLE accounting_account_mappings VALIDATE CONSTRAINT "FKT_accounting_account_mappings_account";

                ALTER TABLE accounting_account_mappings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE accounting_account_mappings FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON accounting_account_mappings TO morrus_app
                    USING ("BusinessId"=app_current_business_id())
                    WITH CHECK ("BusinessId"=app_current_business_id());
                CREATE TRIGGER trg_prevent_business_change BEFORE UPDATE ON accounting_account_mappings
                    FOR EACH ROW EXECUTE FUNCTION app_prevent_business_change();

                REVOKE ALL ON accounting_account_mappings FROM morrus_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON accounting_account_mappings TO morrus_app;
                GRANT SELECT, INSERT ON chart_of_accounts, accounting_account_mappings TO morrus_system;

                -- Snapshot historis: refund lama tetap dapat dilaporkan tanpa
                -- menghitung ulang dari harga yang sudah berubah.
                UPDATE stock_opname_items i
                SET "UnitCostSnapshot"=coalesce(p."CostPrice",0)
                FROM products p
                WHERE p."Id"=i."ProductId" AND i."UnitCostSnapshot"=0;

                UPDATE supplier_returns sr
                SET "SettlementTreatment"=CASE WHEN po."PaymentType"='cash' THEN 'receivable' ELSE 'payable' END
                FROM purchase_orders po
                WHERE po."Id"=sr."PurchaseOrderId";

                WITH totals AS (
                    SELECT t."Id",coalesce(sum(ti."LineTotal"),0) net
                    FROM transactions t
                    LEFT JOIN transaction_items ti ON ti."TransactionId"=t."Id"
                    GROUP BY t."Id"), valued AS (
                    SELECT r."Id",
                           CASE WHEN ti."Qty">0 THEN round(ti."LineTotal"/ti."Qty"*r."Qty",2) ELSE 0 END revenue,
                           totals.net,
                           t."TaxTotal",t."ServiceChargeTotal",
                           round(coalesce(ti."UnitCost",0)*r."Qty",2) cogs,
                           coalesce(p."IsConsignment",false) consignment
                    FROM returns r
                    JOIN transaction_items ti ON ti."Id"=r."TransactionItemId"
                    JOIN transactions t ON t."Id"=r."TransactionId"
                    JOIN totals ON totals."Id"=t."Id"
                    LEFT JOIN products p ON p."Id"=ti."ProductId"
                    WHERE r."RefundAmount"=0)
                UPDATE returns r
                SET "RevenueAmount"=v.revenue,
                    "TaxAmount"=CASE WHEN v.net>0 THEN round(v."TaxTotal"*v.revenue/v.net,2) ELSE 0 END,
                    "ServiceChargeAmount"=CASE WHEN v.net>0 THEN round(v."ServiceChargeTotal"*v.revenue/v.net,2) ELSE 0 END,
                    "CogsAmount"=CASE WHEN v.consignment THEN 0 ELSE v.cogs END,
                    "ConsignmentCogsAmount"=CASE WHEN v.consignment THEN v.cogs ELSE 0 END
                FROM valued v
                WHERE r."Id"=v."Id";
                UPDATE returns
                SET "RefundAmount"="RevenueAmount"+"TaxAmount"+"ServiceChargeAmount"
                WHERE "RefundAmount"=0;

                -- Reversal is the only allowed mutation of a posted journal and
                -- must run through the security-definer function below.
                CREATE OR REPLACE FUNCTION fn_prevent_posted_journal_mutation()
                RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    IF TG_OP='UPDATE' AND TG_TABLE_NAME='journal_entries'
                       AND OLD."Status"='posted' AND NEW."Status"='reversed'
                       AND current_setting('morrus.journal_reversal',true)='on'
                       AND NEW."Id"=OLD."Id" AND NEW."BusinessId"=OLD."BusinessId"
                       AND NEW."OutletId" IS NOT DISTINCT FROM OLD."OutletId"
                       AND NEW."JournalNumber"=OLD."JournalNumber"
                       AND NEW."JournalDate"=OLD."JournalDate"
                       AND NEW."ReferenceType"=OLD."ReferenceType"
                       AND NEW."ReferenceId"=OLD."ReferenceId"
                       AND NEW."Description"=OLD."Description"
                       AND NEW."TotalDebit"=OLD."TotalDebit"
                       AND NEW."TotalCredit"=OLD."TotalCredit"
                       AND NEW."PostingHash"=OLD."PostingHash"
                       AND NEW."ReversalOfJournalId" IS NOT DISTINCT FROM OLD."ReversalOfJournalId"
                       AND NEW."CreatedBy" IS NOT DISTINCT FROM OLD."CreatedBy"
                       AND NEW."CreatedAt"=OLD."CreatedAt"
                    THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'JOURNAL_IMMUTABLE: jurnal posted tidak boleh diubah atau dihapus';
                END
                $fn$;

                CREATE OR REPLACE FUNCTION fn_mark_journal_reversed(p_journal_id uuid)
                RETURNS uuid LANGUAGE plpgsql SECURITY DEFINER SET search_path=public AS $fn$
                DECLARE v_business uuid;
                BEGIN
                    SELECT "BusinessId" INTO v_business FROM journal_entries WHERE "Id"=p_journal_id;
                    IF v_business IS NULL THEN
                        RAISE EXCEPTION 'JOURNAL_REVERSAL_INVALID: jurnal asli tidak ditemukan';
                    END IF;
                    IF app_current_business_id() IS NULL OR v_business<>app_current_business_id() THEN
                        RAISE EXCEPTION 'JOURNAL_REVERSAL_INVALID: jurnal bukan milik tenant aktif';
                    END IF;
                    PERFORM set_config('morrus.journal_reversal','on',true);
                    UPDATE journal_entries SET "Status"='reversed'
                    WHERE "Id"=p_journal_id AND "Status"='posted';
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'JOURNAL_REVERSAL_INVALID: jurnal sudah dibalik';
                    END IF;
                    PERFORM set_config('morrus.journal_reversal','off',true);
                    RETURN p_journal_id;
                END
                $fn$;
                REVOKE ALL ON FUNCTION fn_mark_journal_reversed(uuid) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION fn_mark_journal_reversed(uuid) TO morrus_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS fn_mark_journal_reversed(uuid);
                CREATE OR REPLACE FUNCTION fn_prevent_posted_journal_mutation()
                RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN RAISE EXCEPTION 'JOURNAL_IMMUTABLE: jurnal posted tidak boleh diubah atau dihapus'; END $fn$;
                """);

            migrationBuilder.DropTable(
                name: "accounting_account_mappings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_supplier_returns_SettlementTreatment",
                table: "supplier_returns");

            migrationBuilder.DropColumn(
                name: "SettlementTreatment",
                table: "supplier_returns");

            migrationBuilder.DropColumn(
                name: "UnitCostSnapshot",
                table: "stock_opname_items");

            migrationBuilder.DropColumn(
                name: "CogsAmount",
                table: "returns");

            migrationBuilder.DropColumn(
                name: "ConsignmentCogsAmount",
                table: "returns");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "returns");

            migrationBuilder.DropColumn(
                name: "RevenueAmount",
                table: "returns");

            migrationBuilder.DropColumn(
                name: "ServiceChargeAmount",
                table: "returns");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "returns");
        }
    }
}
