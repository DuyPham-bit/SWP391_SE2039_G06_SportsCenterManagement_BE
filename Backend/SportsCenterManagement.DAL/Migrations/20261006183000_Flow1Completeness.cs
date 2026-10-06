using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsCenterManagement.DAL.Context;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations;

[DbContext(typeof(SportsCenterDbContext))]
[Migration("20261006183000_Flow1Completeness")]
public partial class Flow1Completeness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "provider_reference",
            schema: "dbo",
            table: "payments",
            type: "nvarchar(150)",
            maxLength: 150,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_payments_provider_reference",
            schema: "dbo",
            table: "payments",
            column: "provider_reference",
            unique: true,
            filter: "[provider_reference] IS NOT NULL");

        migrationBuilder.AddColumn<long>(
            name: "center_id",
            schema: "dbo",
            table: "audit_logs",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "attempted_at",
            schema: "dbo",
            table: "payments",
            type: "datetime2",
            nullable: true);

        migrationBuilder.Sql("UPDATE dbo.payments SET attempted_at = paid_at");

        migrationBuilder.AlterColumn<DateTime>(
            name: "attempted_at",
            schema: "dbo",
            table: "payments",
            type: "datetime2",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTime>(
            name: "paid_at",
            schema: "dbo",
            table: "payments",
            type: "datetime2",
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "datetime2");

        migrationBuilder.CreateIndex(
            name: "IX_audit_logs_center_id_created_at",
            schema: "dbo",
            table: "audit_logs",
            columns: new[] { "center_id", "created_at" });

        migrationBuilder.AddForeignKey(
            name: "FK_audit_logs_centers_center_id",
            schema: "dbo",
            table: "audit_logs",
            column: "center_id",
            principalSchema: "dbo",
            principalTable: "centers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_payments_provider_reference",
            schema: "dbo",
            table: "payments");

        migrationBuilder.DropColumn(
            name: "provider_reference",
            schema: "dbo",
            table: "payments");

        migrationBuilder.DropForeignKey(
            name: "FK_audit_logs_centers_center_id",
            schema: "dbo",
            table: "audit_logs");

        migrationBuilder.DropIndex(
            name: "IX_audit_logs_center_id_created_at",
            schema: "dbo",
            table: "audit_logs");

        migrationBuilder.DropColumn(
            name: "center_id",
            schema: "dbo",
            table: "audit_logs");

        migrationBuilder.Sql("UPDATE dbo.payments SET paid_at = attempted_at WHERE paid_at IS NULL");

        migrationBuilder.AlterColumn<DateTime>(
            name: "paid_at",
            schema: "dbo",
            table: "payments",
            type: "datetime2",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "attempted_at",
            schema: "dbo",
            table: "payments");
    }
}
