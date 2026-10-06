using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsCenterManagement.DAL.Context;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations;

[DbContext(typeof(SportsCenterDbContext))]
[Migration("20261006193000_Flow2BookingAndWaitlistHardening")]
public sealed class Flow2BookingAndWaitlistHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "center_id", schema: "dbo", table: "sports",
            type: "bigint", nullable: true);
        migrationBuilder.AddColumn<bool>(
            name: "allow_waitlist", schema: "dbo", table: "classes",
            type: "bit", nullable: false, defaultValue: true);

        migrationBuilder.AddColumn<long>(
            name: "session_id", schema: "dbo", table: "class_waitlist",
            type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(
            name: "subscription_id", schema: "dbo", table: "class_waitlist",
            type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(
            name: "registered_by", schema: "dbo", table: "class_waitlist",
            type: "bigint", nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "subscription_id", schema: "dbo", table: "session_bookings",
            type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(
            name: "registered_by", schema: "dbo", table: "session_bookings",
            type: "bigint", nullable: true);

        migrationBuilder.Sql("""
            UPDATE booking
            SET booking.subscription_id = eligible.Id
            FROM dbo.session_bookings AS booking
            INNER JOIN dbo.class_sessions AS session ON session.Id = booking.session_id
            INNER JOIN dbo.classes AS classEntity ON classEntity.Id = session.class_id
            CROSS APPLY (
                SELECT TOP (1) subscription.Id
                FROM dbo.member_subscriptions AS subscription
                INNER JOIN dbo.membership_packages AS package ON package.Id = subscription.package_id
                WHERE subscription.member_id = booking.member_id
                    AND subscription.status = N'Active'
                    AND subscription.start_date <= session.session_date
                    AND subscription.end_date >= session.session_date
                    AND package.center_id = classEntity.center_id
                ORDER BY subscription.end_date DESC
            ) AS eligible
            WHERE booking.subscription_id IS NULL
                AND booking.status IN (N'Booked', N'CANCELLED_LATE_CHARGED');
            """);
        migrationBuilder.Sql("UPDATE [dbo].[class_waitlist] SET [status] = N'Cancelled' WHERE [status] = N'Waiting';");

        migrationBuilder.CreateIndex(
            name: "IX_sports_center_id",
            schema: "dbo", table: "sports", column: "center_id");
        migrationBuilder.CreateIndex(
            name: "IX_class_waitlist_session_id",
            schema: "dbo", table: "class_waitlist", column: "session_id");
        migrationBuilder.CreateIndex(
            name: "IX_class_waitlist_subscription_id",
            schema: "dbo", table: "class_waitlist", column: "subscription_id");
        migrationBuilder.CreateIndex(
            name: "IX_class_waitlist_registered_by",
            schema: "dbo", table: "class_waitlist", column: "registered_by");
        migrationBuilder.CreateIndex(
            name: "IX_class_waitlist_session_id_member_id",
            schema: "dbo", table: "class_waitlist", columns: new[] { "session_id", "member_id" },
            unique: true, filter: "[session_id] IS NOT NULL AND [status] = N'Waiting'");
        migrationBuilder.CreateIndex(
            name: "IX_session_bookings_registered_by",
            schema: "dbo", table: "session_bookings", column: "registered_by");
        migrationBuilder.CreateIndex(
            name: "IX_session_bookings_subscription_id",
            schema: "dbo", table: "session_bookings", column: "subscription_id");
        migrationBuilder.CreateIndex(
            name: "IX_session_bookings_subscription_id_status_session_id",
            schema: "dbo", table: "session_bookings", columns: new[] { "subscription_id", "status", "session_id" });

        migrationBuilder.AddForeignKey(
            name: "FK_sports_centers_center_id",
            schema: "dbo", table: "sports", column: "center_id",
            principalSchema: "dbo", principalTable: "centers", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_class_waitlist_class_sessions_session_id",
            schema: "dbo", table: "class_waitlist", column: "session_id",
            principalSchema: "dbo", principalTable: "class_sessions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_class_waitlist_member_subscriptions_subscription_id",
            schema: "dbo", table: "class_waitlist", column: "subscription_id",
            principalSchema: "dbo", principalTable: "member_subscriptions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_class_waitlist_users_registered_by",
            schema: "dbo", table: "class_waitlist", column: "registered_by",
            principalSchema: "dbo", principalTable: "users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_session_bookings_member_subscriptions_subscription_id",
            schema: "dbo", table: "session_bookings", column: "subscription_id",
            principalSchema: "dbo", principalTable: "member_subscriptions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_session_bookings_users_registered_by",
            schema: "dbo", table: "session_bookings", column: "registered_by",
            principalSchema: "dbo", principalTable: "users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_sports_centers_center_id", "sports", "dbo");
        migrationBuilder.DropForeignKey("FK_class_waitlist_class_sessions_session_id", "class_waitlist", "dbo");
        migrationBuilder.DropForeignKey("FK_class_waitlist_member_subscriptions_subscription_id", "class_waitlist", "dbo");
        migrationBuilder.DropForeignKey("FK_class_waitlist_users_registered_by", "class_waitlist", "dbo");
        migrationBuilder.DropForeignKey("FK_session_bookings_member_subscriptions_subscription_id", "session_bookings", "dbo");
        migrationBuilder.DropForeignKey("FK_session_bookings_users_registered_by", "session_bookings", "dbo");
        migrationBuilder.DropIndex("IX_class_waitlist_session_id_member_id", "class_waitlist", "dbo");
        migrationBuilder.DropIndex("IX_sports_center_id", "sports", "dbo");
        migrationBuilder.DropIndex("IX_class_waitlist_session_id", "class_waitlist", "dbo");
        migrationBuilder.DropIndex("IX_class_waitlist_subscription_id", "class_waitlist", "dbo");
        migrationBuilder.DropIndex("IX_class_waitlist_registered_by", "class_waitlist", "dbo");
        migrationBuilder.DropIndex("IX_session_bookings_registered_by", "session_bookings", "dbo");
        migrationBuilder.DropIndex("IX_session_bookings_subscription_id", "session_bookings", "dbo");
        migrationBuilder.DropIndex("IX_session_bookings_subscription_id_status_session_id", "session_bookings", "dbo");
        migrationBuilder.DropColumn("session_id", "class_waitlist", "dbo");
        migrationBuilder.DropColumn("subscription_id", "class_waitlist", "dbo");
        migrationBuilder.DropColumn("registered_by", "class_waitlist", "dbo");
        migrationBuilder.DropColumn("subscription_id", "session_bookings", "dbo");
        migrationBuilder.DropColumn("registered_by", "session_bookings", "dbo");
        migrationBuilder.DropColumn("allow_waitlist", "classes", "dbo");
        migrationBuilder.DropColumn("center_id", "sports", "dbo");
    }
}
