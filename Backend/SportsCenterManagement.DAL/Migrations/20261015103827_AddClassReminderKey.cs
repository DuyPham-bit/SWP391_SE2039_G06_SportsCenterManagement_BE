using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddClassReminderKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reminder_key",
                schema: "dbo",
                table: "notifications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_reminder_key",
                schema: "dbo",
                table: "notifications",
                column: "reminder_key",
                unique: true,
                filter: "[reminder_key] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_reminder_key",
                schema: "dbo",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "reminder_key",
                schema: "dbo",
                table: "notifications");
        }
    }
}
