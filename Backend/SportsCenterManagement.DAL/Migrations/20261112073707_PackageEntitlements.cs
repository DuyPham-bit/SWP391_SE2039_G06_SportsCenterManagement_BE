using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class PackageEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cancel_before_hours",
                schema: "dbo",
                table: "membership_packages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_all_access",
                schema: "dbo",
                table: "membership_packages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "max_freeze_days",
                schema: "dbo",
                table: "membership_packages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "tier_level",
                schema: "dbo",
                table: "membership_packages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // One-time backfill from the existing allowed_sports convention (10 = all-access).
            migrationBuilder.Sql("""
                UPDATE dbo.membership_packages SET
                    tier_level = CASE WHEN allowed_sports >= 10 THEN 4 WHEN allowed_sports >= 6 THEN 3
                                      WHEN allowed_sports >= 3 THEN 2 ELSE 1 END,
                    is_all_access = allowed_sports >= 10,
                    cancel_before_hours = CASE WHEN allowed_sports >= 3 THEN 24 ELSE 0 END,
                    max_freeze_days = CASE WHEN allowed_sports >= 10 THEN 60 ELSE 0 END;
                """);

            migrationBuilder.CreateTable(
                name: "subscription_sports",
                schema: "dbo",
                columns: table => new
                {
                    subscription_id = table.Column<long>(type: "bigint", nullable: false),
                    sport_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_sports", x => new { x.subscription_id, x.sport_id });
                    table.ForeignKey(
                        name: "FK_subscription_sports_member_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalSchema: "dbo",
                        principalTable: "member_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subscription_sports_sports_sport_id",
                        column: x => x.sport_id,
                        principalSchema: "dbo",
                        principalTable: "sports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_sports_sport_id",
                schema: "dbo",
                table: "subscription_sports",
                column: "sport_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subscription_sports",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "cancel_before_hours",
                schema: "dbo",
                table: "membership_packages");

            migrationBuilder.DropColumn(
                name: "is_all_access",
                schema: "dbo",
                table: "membership_packages");

            migrationBuilder.DropColumn(
                name: "max_freeze_days",
                schema: "dbo",
                table: "membership_packages");

            migrationBuilder.DropColumn(
                name: "tier_level",
                schema: "dbo",
                table: "membership_packages");
        }
    }
}
