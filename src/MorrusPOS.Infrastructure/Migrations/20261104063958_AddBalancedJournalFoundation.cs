using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBalancedJournalFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                table: "account_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LineNumber",
                table: "account_transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "journal_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    JournalNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    JournalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalDebit = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    TotalCredit = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    PostingHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ReversalOfJournalId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.Id);
                    table.CheckConstraint("CK_journal_entries_Balanced", "\"TotalDebit\" > 0 AND \"TotalCredit\" > 0 AND \"TotalDebit\" = \"TotalCredit\"");
                    table.CheckConstraint("CK_journal_entries_PostingHash", "length(\"PostingHash\") = 64");
                    table.CheckConstraint("CK_journal_entries_Status", "\"Status\" IN ('posted','reversed')");
                    table.ForeignKey(
                        name: "FK_journal_entries_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entries_journal_entries_ReversalOfJournalId",
                        column: x => x.ReversalOfJournalId,
                        principalTable: "journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entries_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entries_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                DO $preflight$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM account_transactions
                        GROUP BY "BusinessId","ReferenceType","ReferenceId",coalesce("TrxNumber",'')
                        HAVING count(*)<2 OR sum("DebitAmount")<=0 OR sum("CreditAmount")<=0
                            OR sum("DebitAmount")<>sum("CreditAmount"))
                    THEN
                        RAISE EXCEPTION 'AddBalancedJournalFoundation gagal: jurnal historis tidak seimbang atau kurang dari dua baris';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM account_transactions
                        WHERE NOT (("DebitAmount">0 AND "CreditAmount"=0) OR ("CreditAmount">0 AND "DebitAmount"=0)))
                    THEN
                        RAISE EXCEPTION 'AddBalancedJournalFoundation gagal: baris jurnal historis memiliki debit/kredit tidak valid';
                    END IF;
                END
                $preflight$;

                WITH groups AS (
                    SELECT "BusinessId","OutletId","ReferenceType","ReferenceId","TrxNumber",
                           min("TrxDate") journal_date,min("CreatedAt") created_at,
                           sum("DebitAmount") total_debit,sum("CreditAmount") total_credit,
                           coalesce("ReferenceId",md5("BusinessId"::text || ':' || "ReferenceType" || ':' || "TrxNumber")::uuid) normalized_ref,
                           md5("BusinessId"::text || ':' || "ReferenceType" || ':' || coalesce("ReferenceId"::text,"TrxNumber"))::uuid journal_id,
                           "BusinessId"::text || ':' || "ReferenceType" || ':' || coalesce("ReferenceId"::text,"TrxNumber") hash_key
                    FROM account_transactions
                    GROUP BY "BusinessId","OutletId","ReferenceType","ReferenceId","TrxNumber")
                INSERT INTO journal_entries
                    ("Id","BusinessId","OutletId","JournalNumber","JournalDate","ReferenceType","ReferenceId",
                     "Description","Status","TotalDebit","TotalCredit","PostingHash","CreatedAt")
                SELECT journal_id,"BusinessId","OutletId","TrxNumber",journal_date,"ReferenceType",normalized_ref,
                       'Backfill jurnal historis ' || "TrxNumber",'posted',total_debit,total_credit,
                       md5(hash_key) || md5('journal:' || hash_key),created_at
                FROM groups;

                WITH ranked AS (
                    SELECT a."Id",
                           md5(a."BusinessId"::text || ':' || a."ReferenceType" || ':' || coalesce(a."ReferenceId"::text,a."TrxNumber"))::uuid journal_id,
                           row_number() OVER (
                               PARTITION BY a."BusinessId",a."ReferenceType",a."ReferenceId",a."TrxNumber"
                               ORDER BY a."CreatedAt",a."Id")::int line_number
                    FROM account_transactions a)
                UPDATE account_transactions a
                SET "JournalEntryId"=r.journal_id,"LineNumber"=r.line_number
                FROM ranked r WHERE r."Id"=a."Id";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "JournalEntryId", table: "account_transactions", type: "uuid", nullable: false,
                oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            migrationBuilder.AlterColumn<int>(
                name: "LineNumber", table: "account_transactions", type: "integer", nullable: false,
                oldClrType: typeof(int), oldType: "integer", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_transactions_BusinessId_ChartOfAccountId_TrxDate",
                table: "account_transactions",
                columns: new[] { "BusinessId", "ChartOfAccountId", "TrxDate" });

            migrationBuilder.CreateIndex(
                name: "IX_account_transactions_JournalEntryId_LineNumber",
                table: "account_transactions",
                columns: new[] { "JournalEntryId", "LineNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_account_transactions_DebitCredit",
                table: "account_transactions",
                sql: "(\"DebitAmount\" > 0 AND \"CreditAmount\" = 0) OR (\"CreditAmount\" > 0 AND \"DebitAmount\" = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_account_transactions_LineNumber",
                table: "account_transactions",
                sql: "\"LineNumber\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_BusinessId_JournalDate",
                table: "journal_entries",
                columns: new[] { "BusinessId", "JournalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_BusinessId_OutletId_JournalDate",
                table: "journal_entries",
                columns: new[] { "BusinessId", "OutletId", "JournalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_BusinessId_ReferenceType_ReferenceId",
                table: "journal_entries",
                columns: new[] { "BusinessId", "ReferenceType", "ReferenceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_CreatedBy",
                table: "journal_entries",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_OutletId",
                table: "journal_entries",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_ReversalOfJournalId",
                table: "journal_entries",
                column: "ReversalOfJournalId");

            migrationBuilder.AddForeignKey(
                name: "FK_account_transactions_journal_entries_JournalEntryId",
                table: "account_transactions",
                column: "JournalEntryId",
                principalTable: "journal_entries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UXT_journal_entries_BusinessId_Id" ON journal_entries ("BusinessId","Id");
                ALTER TABLE journal_entries
                    ADD CONSTRAINT "FKT_journal_entries_outlet"
                    FOREIGN KEY ("BusinessId","OutletId") REFERENCES outlets ("BusinessId","Id") NOT VALID,
                    ADD CONSTRAINT "FKT_journal_entries_user"
                    FOREIGN KEY ("BusinessId","CreatedBy") REFERENCES users ("BusinessId","Id") NOT VALID,
                    ADD CONSTRAINT "FKT_journal_entries_reversal"
                    FOREIGN KEY ("BusinessId","ReversalOfJournalId") REFERENCES journal_entries ("BusinessId","Id") NOT VALID;
                ALTER TABLE journal_entries VALIDATE CONSTRAINT "FKT_journal_entries_outlet";
                ALTER TABLE journal_entries VALIDATE CONSTRAINT "FKT_journal_entries_user";
                ALTER TABLE journal_entries VALIDATE CONSTRAINT "FKT_journal_entries_reversal";
                ALTER TABLE account_transactions
                    ADD CONSTRAINT "FKT_account_transactions_journal"
                    FOREIGN KEY ("BusinessId","JournalEntryId") REFERENCES journal_entries ("BusinessId","Id") NOT VALID;
                ALTER TABLE account_transactions VALIDATE CONSTRAINT "FKT_account_transactions_journal";

                CREATE OR REPLACE FUNCTION fn_validate_journal_balance(p_journal_id uuid)
                RETURNS void LANGUAGE plpgsql AS $fn$
                DECLARE line_count integer; debit numeric; credit numeric; header_debit numeric; header_credit numeric; invalid_count integer;
                BEGIN
                    SELECT count(*),coalesce(sum("DebitAmount"),0),coalesce(sum("CreditAmount"),0)
                    INTO line_count,debit,credit FROM account_transactions WHERE "JournalEntryId"=p_journal_id;
                    SELECT "TotalDebit","TotalCredit" INTO header_debit,header_credit
                    FROM journal_entries WHERE "Id"=p_journal_id;
                    SELECT count(*) INTO invalid_count
                    FROM account_transactions a JOIN journal_entries j ON j."Id"=a."JournalEntryId"
                    WHERE a."JournalEntryId"=p_journal_id
                      AND (a."BusinessId"<>j."BusinessId"
                        OR a."ReferenceType"<>j."ReferenceType"
                        OR a."ReferenceId" IS DISTINCT FROM j."ReferenceId"
                        OR a."OutletId" IS DISTINCT FROM j."OutletId");
                    IF line_count<2 OR debit<=0 OR credit<=0 OR debit<>credit
                       OR debit<>header_debit OR credit<>header_credit OR invalid_count>0 THEN
                        RAISE EXCEPTION 'JOURNAL_UNBALANCED: journal %, lines %, debit %, credit %',p_journal_id,line_count,debit,credit;
                    END IF;
                END
                $fn$;

                CREATE OR REPLACE FUNCTION fn_validate_journal_line_trigger()
                RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    PERFORM fn_validate_journal_balance(coalesce(NEW."JournalEntryId",OLD."JournalEntryId"));
                    RETURN coalesce(NEW,OLD);
                END
                $fn$;
                CREATE CONSTRAINT TRIGGER trg_validate_journal_lines
                    AFTER INSERT OR UPDATE OR DELETE ON account_transactions
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION fn_validate_journal_line_trigger();

                CREATE OR REPLACE FUNCTION fn_validate_journal_header_trigger()
                RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN PERFORM fn_validate_journal_balance(NEW."Id"); RETURN NEW; END $fn$;
                CREATE CONSTRAINT TRIGGER trg_validate_journal_header
                    AFTER INSERT OR UPDATE ON journal_entries
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION fn_validate_journal_header_trigger();

                CREATE OR REPLACE FUNCTION fn_prevent_posted_journal_mutation()
                RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN RAISE EXCEPTION 'JOURNAL_IMMUTABLE: jurnal posted tidak boleh diubah atau dihapus'; END $fn$;
                CREATE TRIGGER trg_prevent_journal_entry_mutation BEFORE UPDATE OR DELETE ON journal_entries
                    FOR EACH ROW EXECUTE FUNCTION fn_prevent_posted_journal_mutation();
                CREATE TRIGGER trg_prevent_journal_line_mutation BEFORE UPDATE OR DELETE ON account_transactions
                    FOR EACH ROW EXECUTE FUNCTION fn_prevent_posted_journal_mutation();

                ALTER TABLE journal_entries ENABLE ROW LEVEL SECURITY;
                ALTER TABLE journal_entries FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON journal_entries TO morrus_app
                    USING ("BusinessId"=app_current_business_id()) WITH CHECK ("BusinessId"=app_current_business_id());
                CREATE TRIGGER trg_prevent_business_change BEFORE UPDATE ON journal_entries
                    FOR EACH ROW EXECUTE FUNCTION app_prevent_business_change();

                REVOKE UPDATE,DELETE ON journal_entries,account_transactions FROM morrus_app;
                GRANT SELECT,INSERT ON journal_entries,account_transactions TO morrus_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_prevent_journal_line_mutation ON account_transactions;
                DROP TRIGGER IF EXISTS trg_prevent_journal_entry_mutation ON journal_entries;
                DROP TRIGGER IF EXISTS trg_validate_journal_lines ON account_transactions;
                DROP TRIGGER IF EXISTS trg_validate_journal_header ON journal_entries;
                DROP TRIGGER IF EXISTS trg_prevent_business_change ON journal_entries;
                DROP POLICY IF EXISTS tenant_isolation ON journal_entries;
                DROP FUNCTION IF EXISTS fn_validate_journal_line_trigger();
                DROP FUNCTION IF EXISTS fn_validate_journal_header_trigger();
                DROP FUNCTION IF EXISTS fn_validate_journal_balance(uuid);
                DROP FUNCTION IF EXISTS fn_prevent_posted_journal_mutation();
                ALTER TABLE account_transactions DROP CONSTRAINT IF EXISTS "FKT_account_transactions_journal";
                GRANT UPDATE,DELETE ON account_transactions TO morrus_app;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_account_transactions_journal_entries_JournalEntryId",
                table: "account_transactions");

            migrationBuilder.DropTable(
                name: "journal_entries");

            migrationBuilder.DropIndex(
                name: "IX_account_transactions_BusinessId_ChartOfAccountId_TrxDate",
                table: "account_transactions");

            migrationBuilder.DropIndex(
                name: "IX_account_transactions_JournalEntryId_LineNumber",
                table: "account_transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_account_transactions_DebitCredit",
                table: "account_transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_account_transactions_LineNumber",
                table: "account_transactions");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "account_transactions");

            migrationBuilder.DropColumn(
                name: "LineNumber",
                table: "account_transactions");
        }
    }
}
