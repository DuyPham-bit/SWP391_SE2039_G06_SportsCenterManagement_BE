using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AuditNotificationCenterMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_refunds_payment_id",
                schema: "dbo",
                table: "payment_refunds");

            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                schema: "dbo",
                table: "payment_refunds",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deduplication_key",
                schema: "dbo",
                table: "notifications",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "time_zone_id",
                schema: "dbo",
                table: "centers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "center_id",
                schema: "dbo",
                table: "audit_logs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "correlation_id",
                schema: "dbo",
                table: "audit_logs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_payment_id_idempotency_key",
                schema: "dbo",
                table: "payment_refunds",
                columns: new[] { "payment_id", "idempotency_key" },
                unique: true,
                filter: "[idempotency_key] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_deduplication_key",
                schema: "dbo",
                table: "notifications",
                column: "deduplication_key",
                unique: true,
                filter: "[deduplication_key] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_center_id_created_at",
                schema: "dbo",
                table: "audit_logs",
                columns: new[] { "center_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_centers_center_id",
                schema: "dbo",
                table: "audit_logs",
                column: "center_id",
                principalSchema: "dbo",
                principalTable: "centers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_centers_center_id",
                schema: "dbo",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_payment_refunds_payment_id_idempotency_key",
                schema: "dbo",
                table: "payment_refunds");

            migrationBuilder.DropIndex(
                name: "IX_notifications_deduplication_key",
                schema: "dbo",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_center_id_created_at",
                schema: "dbo",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                schema: "dbo",
                table: "payment_refunds");

            migrationBuilder.DropColumn(
                name: "deduplication_key",
                schema: "dbo",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "time_zone_id",
                schema: "dbo",
                table: "centers");

            migrationBuilder.DropColumn(
                name: "center_id",
                schema: "dbo",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "correlation_id",
                schema: "dbo",
                table: "audit_logs");

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_payment_id",
                schema: "dbo",
                table: "payment_refunds",
                column: "payment_id");
        }
    }
}
