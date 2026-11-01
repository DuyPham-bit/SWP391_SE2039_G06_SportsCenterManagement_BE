using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsCenterManagement.DAL.Context;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations;

[DbContext(typeof(SportsCenterDbContext))]
[Migration("20261015090000_AddCoachSports")]
public partial class AddCoachSports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "coach_sports",
            schema: "dbo",
            columns: table => new
            {
                coach_id = table.Column<long>(type: "bigint", nullable: false),
                sport_id = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_coach_sports", x => new { x.coach_id, x.sport_id });
                table.ForeignKey("FK_coach_sports_coach_profiles_coach_id", x => x.coach_id,
                    principalSchema: "dbo", principalTable: "coach_profiles", principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_coach_sports_sports_sport_id", x => x.sport_id,
                    principalSchema: "dbo", principalTable: "sports", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_coach_sports_sport_id", schema: "dbo",
            table: "coach_sports", column: "sport_id");

        // Preserve compatible legacy data once; all later writes use explicit sport IDs.
        migrationBuilder.Sql("""
            INSERT INTO [dbo].[coach_sports] ([coach_id], [sport_id])
            SELECT DISTINCT c.[Id], s.[Id]
            FROM [dbo].[coach_profiles] c
            INNER JOIN [dbo].[sports] s ON (s.[center_id] IS NULL OR s.[center_id] = c.[center_id])
            WHERE c.[specialization] IS NOT NULL
              AND CHARINDEX(LOWER(s.[name]), LOWER(c.[specialization])) > 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "coach_sports", schema: "dbo");
}
