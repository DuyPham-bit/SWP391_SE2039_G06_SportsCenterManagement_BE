using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveManagerPerCenterUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_staff_profiles_active_manager_per_center",
                schema: "dbo",
                table: "staff_profiles",
                column: "center_id",
                unique: true,
                filter: "\"position\" = 'Manager' AND \"status\" = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_staff_profiles_active_manager_per_center",
                schema: "dbo",
                table: "staff_profiles");
        }
    }
}
