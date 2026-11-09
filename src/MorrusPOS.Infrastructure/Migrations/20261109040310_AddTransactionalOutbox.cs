using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MorrusPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionalOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNo = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SchemaVersion = table.Column<short>(type: "smallint", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    DeduplicationKey = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AvailableAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LockedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockId = table.Column<Guid>(type: "uuid", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeadLetteredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ManualReplayCount = table.Column<int>(type: "integer", nullable: false),
                    LastReplayedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastReplayedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    LastReplayReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                    table.CheckConstraint("CK_outbox_messages_Attempts", "\"AttemptCount\" BETWEEN 0 AND \"MaxAttempts\" AND \"MaxAttempts\" BETWEEN 1 AND 20 AND \"ManualReplayCount\" >= 0");
                    table.CheckConstraint("CK_outbox_messages_CompletedState", "(\"Status\" <> 'processed' OR \"ProcessedAtUtc\" IS NOT NULL) AND (\"Status\" <> 'dead_letter' OR \"DeadLetteredAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_outbox_messages_LockState", "(\"Status\" = 'processing' AND \"LockId\" IS NOT NULL AND \"LockedAtUtc\" IS NOT NULL AND \"LockedBy\" IS NOT NULL) OR (\"Status\" <> 'processing' AND \"LockId\" IS NULL AND \"LockedAtUtc\" IS NULL AND \"LockedBy\" IS NULL)");
                    table.CheckConstraint("CK_outbox_messages_PayloadSize", "octet_length(\"PayloadJson\"::text) <= 262144");
                    table.CheckConstraint("CK_outbox_messages_Status", "\"Status\" IN ('pending','processing','processed','failed','dead_letter')");
                    table.ForeignKey(
                        name: "FK_outbox_messages_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_outbox_messages_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_BusinessId_EventType_DeduplicationKey",
                table: "outbox_messages",
                columns: new[] { "BusinessId", "EventType", "DeduplicationKey" },
                unique: true,
                filter: "\"DeduplicationKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_BusinessId_OutletId_OccurredAtUtc",
                table: "outbox_messages",
                columns: new[] { "BusinessId", "OutletId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_OutletId",
                table: "outbox_messages",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_SequenceNo",
                table: "outbox_messages",
                column: "SequenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_Status_AvailableAtUtc_SequenceNo",
                table: "outbox_messages",
                columns: new[] { "Status", "AvailableAtUtc", "SequenceNo" },
                filter: "\"Status\" IN ('pending','failed')");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_Status_LockedAtUtc",
                table: "outbox_messages",
                columns: new[] { "Status", "LockedAtUtc" },
                filter: "\"Status\" = 'processing'");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_Status_OccurredAtUtc",
                table: "outbox_messages",
                columns: new[] { "Status", "OccurredAtUtc" });

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UXT_outbox_messages_BusinessId_Id"
                    ON outbox_messages ("BusinessId","Id");
                ALTER TABLE outbox_messages
                    ADD CONSTRAINT "FKT_outbox_messages_outlet"
                    FOREIGN KEY ("BusinessId","OutletId")
                    REFERENCES outlets ("BusinessId","Id") NOT VALID;
                ALTER TABLE outbox_messages VALIDATE CONSTRAINT "FKT_outbox_messages_outlet";

                CREATE OR REPLACE FUNCTION fn_guard_outbox_immutable_payload()
                RETURNS trigger
                LANGUAGE plpgsql
                SET search_path=pg_catalog,public
                AS $fn$
                BEGIN
                    IF NEW."Id" IS DISTINCT FROM OLD."Id"
                       OR NEW."SequenceNo" IS DISTINCT FROM OLD."SequenceNo"
                       OR NEW."BusinessId" IS DISTINCT FROM OLD."BusinessId"
                       OR NEW."OutletId" IS DISTINCT FROM OLD."OutletId"
                       OR NEW."EventType" IS DISTINCT FROM OLD."EventType"
                       OR NEW."SchemaVersion" IS DISTINCT FROM OLD."SchemaVersion"
                       OR NEW."PayloadJson" IS DISTINCT FROM OLD."PayloadJson"
                       OR NEW."DeduplicationKey" IS DISTINCT FROM OLD."DeduplicationKey"
                       OR NEW."CorrelationId" IS DISTINCT FROM OLD."CorrelationId"
                       OR NEW."OccurredAtUtc" IS DISTINCT FROM OLD."OccurredAtUtc"
                       OR NEW."MaxAttempts" IS DISTINCT FROM OLD."MaxAttempts"
                    THEN
                        RAISE EXCEPTION 'OUTBOX_IMMUTABLE: event identity dan payload tidak boleh diubah';
                    END IF;
                    RETURN NEW;
                END
                $fn$;
                CREATE TRIGGER trg_guard_outbox_immutable_payload
                    BEFORE UPDATE ON outbox_messages
                    FOR EACH ROW EXECUTE FUNCTION fn_guard_outbox_immutable_payload();

                ALTER TABLE outbox_messages ENABLE ROW LEVEL SECURITY;
                ALTER TABLE outbox_messages FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON outbox_messages TO morrus_app
                    USING ("BusinessId"=app_current_business_id())
                    WITH CHECK ("BusinessId"=app_current_business_id());

                REVOKE ALL ON outbox_messages FROM morrus_app, morrus_system;
                GRANT INSERT ON outbox_messages TO morrus_app;
                GRANT SELECT ("BusinessId","EventType","DeduplicationKey") ON outbox_messages TO morrus_app;
                GRANT SELECT, UPDATE, DELETE ON outbox_messages TO morrus_system;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_guard_outbox_immutable_payload();");
        }
    }
}
