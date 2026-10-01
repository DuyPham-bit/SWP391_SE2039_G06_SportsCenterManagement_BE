using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class CoreFlowUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_session_bookings_session_id",
                schema: "dbo",
                table: "session_bookings");

            migrationBuilder.DropIndex(
                name: "IX_class_enrollments_class_id",
                schema: "dbo",
                table: "class_enrollments");

            migrationBuilder.CreateIndex(
                name: "IX_session_bookings_session_id_member_id",
                schema: "dbo",
                table: "session_bookings",
                columns: new[] { "session_id", "member_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_transaction_code",
                schema: "dbo",
                table: "payments",
                column: "transaction_code",
                unique: true,
                filter: "[transaction_code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_class_enrollments_class_id_member_id",
                schema: "dbo",
                table: "class_enrollments",
                columns: new[] { "class_id", "member_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_session_bookings_session_id_member_id",
                schema: "dbo",
                table: "session_bookings");

            migrationBuilder.DropIndex(
                name: "IX_payments_transaction_code",
                schema: "dbo",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_class_enrollments_class_id_member_id",
                schema: "dbo",
                table: "class_enrollments");

            migrationBuilder.CreateIndex(
                name: "IX_session_bookings_session_id",
                schema: "dbo",
                table: "session_bookings",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_enrollments_class_id",
                schema: "dbo",
                table: "class_enrollments",
                column: "class_id");
        }
    }
}
