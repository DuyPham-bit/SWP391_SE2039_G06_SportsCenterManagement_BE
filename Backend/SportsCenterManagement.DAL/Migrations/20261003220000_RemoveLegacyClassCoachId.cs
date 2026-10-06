using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsCenterManagement.DAL.Context;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations;

[DbContext(typeof(SportsCenterDbContext))]
[Migration("20261003220000_RemoveLegacyClassCoachId")]
public sealed class RemoveLegacyClassCoachId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'dbo.class_coaches', N'Id') IS NOT NULL
            BEGIN
                ALTER TABLE [dbo].[class_coaches] DROP COLUMN [Id];
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "Id",
            schema: "dbo",
            table: "class_coaches",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);
    }
}
