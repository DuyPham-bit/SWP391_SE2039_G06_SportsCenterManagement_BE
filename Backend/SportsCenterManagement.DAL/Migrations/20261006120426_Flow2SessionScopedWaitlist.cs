using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Flow2SessionScopedWaitlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "booked_by",
                schema: "dbo",
                table: "session_bookings",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "subscription_id",
                schema: "dbo",
                table: "session_bookings",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "joined_by",
                schema: "dbo",
                table: "class_waitlist",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "session_id",
                schema: "dbo",
                table: "class_waitlist",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "subscription_id",
                schema: "dbo",
                table: "class_waitlist",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_bookings_booked_by",
                schema: "dbo",
                table: "session_bookings",
                column: "booked_by");

            migrationBuilder.CreateIndex(
                name: "IX_session_bookings_subscription_id",
                schema: "dbo",
                table: "session_bookings",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_waitlist_joined_by",
                schema: "dbo",
                table: "class_waitlist",
                column: "joined_by");

            migrationBuilder.CreateIndex(
                name: "IX_class_waitlist_session_id",
                schema: "dbo",
                table: "class_waitlist",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_waitlist_session_id_member_id",
                schema: "dbo",
                table: "class_waitlist",
                columns: new[] { "session_id", "member_id" },
                unique: true,
                filter: "[session_id] IS NOT NULL AND [status] = N'Waiting'");

            migrationBuilder.CreateIndex(
                name: "IX_class_waitlist_subscription_id",
                schema: "dbo",
                table: "class_waitlist",
                column: "subscription_id");

            migrationBuilder.AddForeignKey(
                name: "FK_class_waitlist_class_sessions_session_id",
                schema: "dbo",
                table: "class_waitlist",
                column: "session_id",
                principalSchema: "dbo",
                principalTable: "class_sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_class_waitlist_member_subscriptions_subscription_id",
                schema: "dbo",
                table: "class_waitlist",
                column: "subscription_id",
                principalSchema: "dbo",
                principalTable: "member_subscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_class_waitlist_users_joined_by",
                schema: "dbo",
                table: "class_waitlist",
                column: "joined_by",
                principalSchema: "dbo",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_session_bookings_member_subscriptions_subscription_id",
                schema: "dbo",
                table: "session_bookings",
                column: "subscription_id",
                principalSchema: "dbo",
                principalTable: "member_subscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_session_bookings_users_booked_by",
                schema: "dbo",
                table: "session_bookings",
                column: "booked_by",
                principalSchema: "dbo",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_class_waitlist_class_sessions_session_id",
                schema: "dbo",
                table: "class_waitlist");

            migrationBuilder.DropForeignKey(
                name: "FK_class_waitlist_member_subscriptions_subscription_id",
                schema: "dbo",
                table: "class_waitlist");

            migrationBuilder.DropForeignKey(
                name: "FK_class_waitlist_users_joined_by",
                schema: "dbo",
                table: "class_waitlist");

            migrationBuilder.DropForeignKey(
                name: "FK_session_bookings_member_subscriptions_subscription_id",
                schema: "dbo",
                table: "session_bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_session_bookings_users_booked_by",
                schema: "dbo",
                table: "session_bookings");

            migrationBuilder.DropIndex(
                name: "IX_session_bookings_booked_by",
                schema: "dbo",
                table: "session_bookings");

            migrationBuilder.DropIndex(
                name: "IX_session_bookings_subscription_id",
                schema: "dbo",
                table: "session_bookings");

            migrationBuilder.DropIndex(
                name: "IX_class_waitlist_joined_by",
                schema: "dbo",
                table: "class_waitlist");

            migrationBuilder.DropIndex(
                name: "IX_class_waitlist_session_id",
                schema: "dbo",
                table: "class_waitlist");

            migrationBuilder.DropIndex(
                name: "IX_class_waitlist_session_id_member_id",
                schema: "dbo",
                table: "class_waitlist");

            migrationBuilder.DropIndex(
                name: "IX_class_waitlist_subscription_id",
                schema: "dbo",
                table: "class_waitlist");

            migrationBuilder.DropColumn(
                name: "booked_by",
                schema: "dbo",
                table: "session_bookings");

            migrationBuilder.DropColumn(
                name: "subscription_id",
                schema: "dbo",
                table: "session_bookings");

            migrationBuilder.DropColumn(
                name: "joined_by",
                schema: "dbo",
                table: "class_waitlist");

            migrationBuilder.DropColumn(
                name: "session_id",
                schema: "dbo",
                table: "class_waitlist");

            migrationBuilder.DropColumn(
                name: "subscription_id",
                schema: "dbo",
                table: "class_waitlist");
        }
    }
}
