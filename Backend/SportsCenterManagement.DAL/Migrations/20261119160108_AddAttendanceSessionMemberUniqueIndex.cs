using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceSessionMemberUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fail before DDL if legacy data violates the business key; never delete attendance history implicitly.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM dbo.attendance
                        GROUP BY session_id, member_id
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot create attendance unique index: duplicate (session_id, member_id) rows exist';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_attendance_session_id",
                schema: "dbo",
                table: "attendance");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_session_id_member_id",
                schema: "dbo",
                table: "attendance",
                columns: new[] { "session_id", "member_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_attendance_session_id_member_id",
                schema: "dbo",
                table: "attendance");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_session_id",
                schema: "dbo",
                table: "attendance",
                column: "session_id");
        }
    }
}
