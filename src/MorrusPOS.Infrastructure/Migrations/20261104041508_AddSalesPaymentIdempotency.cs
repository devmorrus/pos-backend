using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesPaymentIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_returns_BusinessId",
                table: "returns");

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "transactions",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "supplier_payments",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RefundRequestId",
                table: "returns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RequestHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.Id);
                    table.CheckConstraint("CK_idempotency_records_Operation", "\"Operation\" IN ('checkout','refund','supplier_payment')");
                    table.CheckConstraint("CK_idempotency_records_RequestHash", "length(\"RequestHash\") = 64");
                    table.ForeignKey(
                        name: "FK_idempotency_records_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_idempotency_records_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_idempotency_records_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                UPDATE returns SET "RefundRequestId"="Id" WHERE "RefundRequestId" IS NULL;

                DO $preflight$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM payments
                        WHERE "ReferenceNumber" IS NOT NULL AND "Method" IN ('qris','transfer','edc')
                        GROUP BY "BusinessId",lower("Method"),upper(trim("ReferenceNumber"))
                        HAVING count(*)>1)
                    THEN
                        RAISE EXCEPTION 'AddSalesPaymentIdempotency gagal: duplicate reference pembayaran POS ditemukan';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM supplier_payments
                        WHERE "ReferenceNumber" IS NOT NULL
                        GROUP BY "BusinessId",lower("PaymentMethod"),upper(trim("ReferenceNumber"))
                        HAVING count(*)>1)
                    THEN
                        RAISE EXCEPTION 'AddSalesPaymentIdempotency gagal: duplicate reference pembayaran supplier ditemukan';
                    END IF;
                END
                $preflight$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "RefundRequestId",
                table: "returns",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_returns_BusinessId_RefundRequestId",
                table: "returns",
                columns: new[] { "BusinessId", "RefundRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_returns_BusinessId_TransactionId_RefundRequestId",
                table: "returns",
                columns: new[] { "BusinessId", "TransactionId", "RefundRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_BusinessId_Operation_IdempotencyKey",
                table: "idempotency_records",
                columns: new[] { "BusinessId", "Operation", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_BusinessId_OutletId_CreatedAt",
                table: "idempotency_records",
                columns: new[] { "BusinessId", "OutletId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_BusinessId_ResourceType_ResourceId",
                table: "idempotency_records",
                columns: new[] { "BusinessId", "ResourceType", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_OutletId",
                table: "idempotency_records",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_UserId",
                table: "idempotency_records",
                column: "UserId");

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_payments_Business_Method_Reference"
                    ON payments ("BusinessId",lower("Method"),upper(trim("ReferenceNumber")))
                    WHERE "ReferenceNumber" IS NOT NULL AND "Method" IN ('qris','transfer','edc');
                CREATE UNIQUE INDEX "UX_supplier_payments_Business_Method_Reference"
                    ON supplier_payments ("BusinessId",lower("PaymentMethod"),upper(trim("ReferenceNumber")))
                    WHERE "ReferenceNumber" IS NOT NULL;

                CREATE UNIQUE INDEX "UXT_idempotency_records_BusinessId_Id"
                    ON idempotency_records ("BusinessId","Id");
                ALTER TABLE idempotency_records
                    ADD CONSTRAINT "FKT_idempotency_records_user"
                    FOREIGN KEY ("BusinessId","UserId") REFERENCES users ("BusinessId","Id") NOT VALID,
                    ADD CONSTRAINT "FKT_idempotency_records_outlet"
                    FOREIGN KEY ("BusinessId","OutletId") REFERENCES outlets ("BusinessId","Id") NOT VALID;
                ALTER TABLE idempotency_records VALIDATE CONSTRAINT "FKT_idempotency_records_user";
                ALTER TABLE idempotency_records VALIDATE CONSTRAINT "FKT_idempotency_records_outlet";

                ALTER TABLE idempotency_records ENABLE ROW LEVEL SECURITY;
                ALTER TABLE idempotency_records FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON idempotency_records TO morrus_app
                    USING ("BusinessId"=app_current_business_id())
                    WITH CHECK ("BusinessId"=app_current_business_id());

                CREATE OR REPLACE FUNCTION fn_prevent_idempotency_mutation()
                RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    RAISE EXCEPTION 'IDEMPOTENCY_RECORD_IMMUTABLE: receipt tidak boleh diubah atau dihapus';
                END
                $fn$;
                CREATE TRIGGER trg_prevent_idempotency_mutation
                    BEFORE UPDATE OR DELETE ON idempotency_records
                    FOR EACH ROW EXECUTE FUNCTION fn_prevent_idempotency_mutation();

                REVOKE ALL ON idempotency_records FROM morrus_app;
                GRANT SELECT,INSERT ON idempotency_records TO morrus_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "UX_payments_Business_Method_Reference";
                DROP INDEX IF EXISTS "UX_supplier_payments_Business_Method_Reference";
                DROP TRIGGER IF EXISTS trg_prevent_idempotency_mutation ON idempotency_records;
                DROP FUNCTION IF EXISTS fn_prevent_idempotency_mutation();
                DROP POLICY IF EXISTS tenant_isolation ON idempotency_records;
                """);
            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropIndex(
                name: "IX_returns_BusinessId_RefundRequestId",
                table: "returns");

            migrationBuilder.DropIndex(
                name: "IX_returns_BusinessId_TransactionId_RefundRequestId",
                table: "returns");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "supplier_payments");

            migrationBuilder.DropColumn(
                name: "RefundRequestId",
                table: "returns");

            migrationBuilder.CreateIndex(
                name: "IX_returns_BusinessId",
                table: "returns",
                column: "BusinessId");

        }
    }
}
