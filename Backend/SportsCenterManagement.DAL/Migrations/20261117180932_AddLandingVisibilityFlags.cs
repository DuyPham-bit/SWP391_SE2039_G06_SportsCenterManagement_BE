using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <summary>Landing-page visibility of sports and rooms; existing rows stay visible.</summary>
    public partial class AddLandingVisibilityFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_visible_on_landing",
                schema: "dbo",
                table: "sports",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_visible_on_landing",
                schema: "dbo",
                table: "rooms",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "is_visible_on_landing", schema: "dbo", table: "sports");
            migrationBuilder.DropColumn(name: "is_visible_on_landing", schema: "dbo", table: "rooms");
        }
    }
}
