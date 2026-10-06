using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Flow1MembershipScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_membership_packages_center_id",
                schema: "dbo",
                table: "membership_packages");

            migrationBuilder.AddColumn<long>(
                name: "center_id",
                schema: "dbo",
                table: "member_profiles",
                type: "bigint",
                nullable: true);

            // Chỉ gán center khi lịch sử chỉ ra duy nhất một nơi để tránh tự chọn sai center.
            migrationBuilder.Sql("""
                WITH member_centers AS
                (
                    SELECT member_id, MIN(center_id) AS center_id
                    FROM dbo.invoices
                    GROUP BY member_id
                    HAVING COUNT(DISTINCT center_id) = 1
                )
                UPDATE profile
                SET center_id = member_centers.center_id
                FROM dbo.member_profiles AS profile
                INNER JOIN member_centers ON member_centers.member_id = profile.id;

                WITH subscription_centers AS
                (
                    SELECT subscription.member_id, MIN(package.center_id) AS center_id
                    FROM dbo.member_subscriptions AS subscription
                    INNER JOIN dbo.membership_packages AS package ON package.id = subscription.package_id
                    GROUP BY subscription.member_id
                    HAVING COUNT(DISTINCT package.center_id) = 1
                )
                UPDATE profile
                SET center_id = subscription_centers.center_id
                FROM dbo.member_profiles AS profile
                INNER JOIN subscription_centers ON subscription_centers.member_id = profile.id
                WHERE profile.center_id IS NULL
                  AND NOT EXISTS (SELECT 1 FROM dbo.invoices AS invoice WHERE invoice.member_id = profile.id);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_users_phone",
                schema: "dbo",
                table: "users",
                column: "phone",
                unique: true,
                filter: "[phone] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_membership_packages_center_id_name",
                schema: "dbo",
                table: "membership_packages",
                columns: new[] { "center_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_member_profiles_center_id",
                schema: "dbo",
                table: "member_profiles",
                column: "center_id");

            migrationBuilder.AddForeignKey(
                name: "FK_member_profiles_centers_center_id",
                schema: "dbo",
                table: "member_profiles",
                column: "center_id",
                principalSchema: "dbo",
                principalTable: "centers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_member_profiles_centers_center_id",
                schema: "dbo",
                table: "member_profiles");

            migrationBuilder.DropIndex(
                name: "IX_users_phone",
                schema: "dbo",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_membership_packages_center_id_name",
                schema: "dbo",
                table: "membership_packages");

            migrationBuilder.DropIndex(
                name: "IX_member_profiles_center_id",
                schema: "dbo",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "center_id",
                schema: "dbo",
                table: "member_profiles");

            migrationBuilder.CreateIndex(
                name: "IX_membership_packages_center_id",
                schema: "dbo",
                table: "membership_packages",
                column: "center_id");
        }
    }
}
