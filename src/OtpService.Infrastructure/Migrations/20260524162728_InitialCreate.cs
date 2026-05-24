using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OtpService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "otp_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tracking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    msisdn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    code_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    salt = table.Column<byte[]>(type: "bytea", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    max_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    status = table.Column<int>(type: "integer", nullable: false),
                    whatsapp_message_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    last_applied_timestamp = table.Column<long>(type: "bigint", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_otp_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_otp_records_msisdn",
                table: "otp_records",
                column: "msisdn");

            migrationBuilder.CreateIndex(
                name: "ix_otp_records_request_id",
                table: "otp_records",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_otp_records_status",
                table: "otp_records",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_otp_records_tracking_id",
                table: "otp_records",
                column: "tracking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_otp_records_whatsapp_message_id",
                table: "otp_records",
                column: "whatsapp_message_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "otp_records");
        }
    }
}
