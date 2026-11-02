using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class SupportRequestSenders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "member_id",
                schema: "dbo",
                table: "support_requests",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "center_id",
                schema: "dbo",
                table: "support_requests",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "requester_id",
                schema: "dbo",
                table: "support_requests",
                type: "bigint",
                nullable: true);

            // Existing requests were always submitted for a member; preserve their owner and center.
            migrationBuilder.Sql("""
                UPDATE request
                SET request.[requester_id] = member.[user_id], request.[center_id] = member.[center_id]
                FROM [dbo].[support_requests] request
                INNER JOIN [dbo].[member_profiles] member ON member.[Id] = request.[member_id];
                IF EXISTS (SELECT 1 FROM [dbo].[support_requests] WHERE [requester_id] IS NULL OR [center_id] IS NULL)
                    THROW 51000, 'Support request owner or center is missing; repair legacy records before migrating.', 1;
                """);

            migrationBuilder.AlterColumn<long>(name: "center_id", schema: "dbo", table: "support_requests",
                type: "bigint", nullable: false, oldClrType: typeof(long), oldType: "bigint", oldNullable: true);
            migrationBuilder.AlterColumn<long>(name: "requester_id", schema: "dbo", table: "support_requests",
                type: "bigint", nullable: false, oldClrType: typeof(long), oldType: "bigint", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_support_requests_center_id_status_created_at",
                schema: "dbo",
                table: "support_requests",
                columns: new[] { "center_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_support_requests_requester_id_created_at",
                schema: "dbo",
                table: "support_requests",
                columns: new[] { "requester_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_support_requests_centers_center_id",
                schema: "dbo",
                table: "support_requests",
                column: "center_id",
                principalSchema: "dbo",
                principalTable: "centers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_support_requests_users_requester_id",
                schema: "dbo",
                table: "support_requests",
                column: "requester_id",
                principalSchema: "dbo",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old schema cannot represent coach requests. Never delete them to make a downgrade pass.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [dbo].[support_requests] WHERE [member_id] IS NULL)
                    THROW 51000, 'Coach support requests exist; keep this migration to preserve those records.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_support_requests_centers_center_id",
                schema: "dbo",
                table: "support_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_support_requests_users_requester_id",
                schema: "dbo",
                table: "support_requests");

            migrationBuilder.DropIndex(
                name: "IX_support_requests_center_id_status_created_at",
                schema: "dbo",
                table: "support_requests");

            migrationBuilder.DropIndex(
                name: "IX_support_requests_requester_id_created_at",
                schema: "dbo",
                table: "support_requests");

            migrationBuilder.DropColumn(
                name: "center_id",
                schema: "dbo",
                table: "support_requests");

            migrationBuilder.DropColumn(
                name: "requester_id",
                schema: "dbo",
                table: "support_requests");

            migrationBuilder.AlterColumn<long>(
                name: "member_id",
                schema: "dbo",
                table: "support_requests",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
