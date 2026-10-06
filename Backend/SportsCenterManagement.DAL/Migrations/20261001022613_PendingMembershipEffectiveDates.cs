using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class PendingMembershipEffectiveDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "start_date",
                schema: "dbo",
                table: "member_subscriptions",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "end_date",
                schema: "dbo",
                table: "member_subscriptions",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<int>(
                name: "duration_days",
                schema: "dbo",
                table: "member_subscriptions",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE subscription
                SET duration_days = package.duration_days
                FROM dbo.member_subscriptions AS subscription
                INNER JOIN dbo.membership_packages AS package
                    ON package.id = subscription.package_id;

                UPDATE dbo.member_subscriptions
                SET start_date = NULL, end_date = NULL
                WHERE status = 'PendingPayment';
                """);

            migrationBuilder.AlterColumn<int>(
                name: "duration_days",
                schema: "dbo",
                table: "member_subscriptions",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE dbo.member_subscriptions
                SET start_date = CAST(created_at AS date),
                    end_date = DATEADD(day, duration_days - 1, CAST(created_at AS date))
                WHERE start_date IS NULL OR end_date IS NULL;
                """);

            migrationBuilder.DropColumn(
                name: "duration_days",
                schema: "dbo",
                table: "member_subscriptions");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "start_date",
                schema: "dbo",
                table: "member_subscriptions",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "end_date",
                schema: "dbo",
                table: "member_subscriptions",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}
