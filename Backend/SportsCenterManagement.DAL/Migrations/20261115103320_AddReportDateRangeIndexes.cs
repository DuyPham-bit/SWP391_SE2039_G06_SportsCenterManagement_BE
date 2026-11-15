using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddReportDateRangeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_center_checkins_center_id",
                schema: "dbo",
                table: "center_checkins");

            migrationBuilder.CreateIndex(
                name: "IX_payments_paid_at",
                schema: "dbo",
                table: "payments",
                column: "paid_at");

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_processed_at",
                schema: "dbo",
                table: "payment_refunds",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "IX_center_checkins_center_id_check_in_time",
                schema: "dbo",
                table: "center_checkins",
                columns: new[] { "center_id", "check_in_time" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payments_paid_at",
                schema: "dbo",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payment_refunds_processed_at",
                schema: "dbo",
                table: "payment_refunds");

            migrationBuilder.DropIndex(
                name: "IX_center_checkins_center_id_check_in_time",
                schema: "dbo",
                table: "center_checkins");

            migrationBuilder.CreateIndex(
                name: "IX_center_checkins_center_id",
                schema: "dbo",
                table: "center_checkins",
                column: "center_id");
        }
    }
}
