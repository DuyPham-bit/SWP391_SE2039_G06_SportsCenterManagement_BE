using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsCenterManagement.DAL.Context;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations;

[DbContext(typeof(SportsCenterDbContext))]
[Migration("20261003210000_Uc13AvailabilityAndPermissionModules")]
public sealed class Uc13AvailabilityAndPermissionModules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "module",
            schema: "dbo",
            table: "permissions",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "coach_leaves",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                coach_id = table.Column<long>(type: "bigint", nullable: false),
                start_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                end_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_coach_leaves", row => row.Id);
                table.ForeignKey(
                    name: "FK_coach_leaves_coach_profiles_coach_id",
                    column: row => row.coach_id,
                    principalSchema: "dbo",
                    principalTable: "coach_profiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_coach_leaves_coach_id",
            schema: "dbo",
            table: "coach_leaves",
            column: "coach_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "coach_leaves", schema: "dbo");
        migrationBuilder.DropColumn(name: "module", schema: "dbo", table: "permissions");
    }
}
