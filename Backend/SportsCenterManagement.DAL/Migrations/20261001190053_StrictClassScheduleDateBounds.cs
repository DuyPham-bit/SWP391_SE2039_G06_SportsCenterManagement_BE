using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class StrictClassScheduleDateBounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_class_schedules_date_range",
                schema: "dbo",
                table: "class_schedules");

            migrationBuilder.AddCheckConstraint(
                name: "CK_class_schedules_date_range",
                schema: "dbo",
                table: "class_schedules",
                sql: "([start_date] IS NULL AND [end_date] IS NULL) OR ([start_date] IS NOT NULL AND [end_date] IS NOT NULL AND [start_date] <= [end_date])");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_class_schedules_date_range",
                schema: "dbo",
                table: "class_schedules");

            migrationBuilder.AddCheckConstraint(
                name: "CK_class_schedules_date_range",
                schema: "dbo",
                table: "class_schedules",
                sql: "[start_date] IS NULL OR [end_date] IS NULL OR [start_date] <= [end_date]");
        }
    }
}
