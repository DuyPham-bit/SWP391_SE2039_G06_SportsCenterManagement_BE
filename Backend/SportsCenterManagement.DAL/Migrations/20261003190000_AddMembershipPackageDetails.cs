using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SportsCenterManagement.DAL.Context;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations;

[DbContext(typeof(SportsCenterDbContext))]
[Migration("20261003190000_AddMembershipPackageDetails")]
public partial class AddMembershipPackageDetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "allowed_sports",
            schema: "dbo",
            table: "membership_packages",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<string>(
            name: "badge",
            schema: "dbo",
            table: "membership_packages",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "features",
            schema: "dbo",
            table: "membership_packages",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "allowed_sports", schema: "dbo", table: "membership_packages");
        migrationBuilder.DropColumn(name: "badge", schema: "dbo", table: "membership_packages");
        migrationBuilder.DropColumn(name: "features", schema: "dbo", table: "membership_packages");
    }
}
