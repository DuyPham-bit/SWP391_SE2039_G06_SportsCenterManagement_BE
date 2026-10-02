using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class BackendRequirementSchemaHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.attendance GROUP BY session_id, member_id HAVING COUNT(*) > 1)
                    THROW 51000, 'Schema hardening blocked: duplicate attendance rows exist for a session/member.', 1;
                IF EXISTS (SELECT 1 FROM dbo.user_notifications GROUP BY notification_id, user_id HAVING COUNT(*) > 1)
                    THROW 51000, 'Schema hardening blocked: duplicate notification recipients exist.', 1;
                IF EXISTS (SELECT 1 FROM dbo.center_checkins WHERE check_out_time IS NULL GROUP BY center_id, member_id HAVING COUNT(*) > 1)
                    THROW 51000, 'Schema hardening blocked: duplicate open member check-ins exist.', 1;
                IF EXISTS (SELECT 1 FROM dbo.class_coaches WHERE is_primary = 1 GROUP BY class_id HAVING COUNT(*) > 1)
                    THROW 51000, 'Schema hardening blocked: a class has multiple primary coaches.', 1;
                IF EXISTS (SELECT 1 FROM dbo.classes WHERE capacity <= 0 OR duration_minutes <= 0)
                    THROW 51000, 'Schema hardening blocked: class capacity/duration must be positive.', 1;
                IF EXISTS (SELECT 1 FROM dbo.class_schedules WHERE day_of_week NOT BETWEEN 0 AND 6 OR start_time >= end_time OR (start_date IS NULL AND end_date IS NOT NULL) OR (start_date IS NOT NULL AND end_date IS NULL) OR start_date > end_date)
                    THROW 51000, 'Schema hardening blocked: class schedule date/time values are invalid.', 1;
                IF EXISTS (SELECT 1 FROM dbo.class_sessions WHERE start_time >= end_time)
                    THROW 51000, 'Schema hardening blocked: class session end time must follow start time.', 1;
                IF EXISTS (SELECT 1 FROM dbo.rooms WHERE capacity <= 0)
                    THROW 51000, 'Schema hardening blocked: room capacity must be positive.', 1;
                IF EXISTS (SELECT 1 FROM dbo.membership_packages WHERE duration_days <= 0 OR price < 0 OR max_classes <= 0)
                    THROW 51000, 'Schema hardening blocked: membership package values are invalid.', 1;
                IF EXISTS (SELECT 1 FROM dbo.member_subscriptions WHERE duration_days <= 0 OR price < 0 OR (start_date IS NULL AND end_date IS NOT NULL) OR (start_date IS NOT NULL AND end_date IS NULL) OR start_date > end_date)
                    THROW 51000, 'Schema hardening blocked: member subscription values are invalid.', 1;
                IF EXISTS (SELECT 1 FROM dbo.invoices WHERE subtotal < 0 OR discount < 0 OR tax < 0 OR total_amount < 0 OR discount > subtotal OR total_amount <> subtotal - discount + tax)
                    THROW 51000, 'Schema hardening blocked: invoice totals do not match their components.', 1;
                IF EXISTS (SELECT 1 FROM dbo.invoice_items WHERE quantity <= 0 OR unit_price < 0 OR amount < 0)
                    THROW 51000, 'Schema hardening blocked: invoice item values are invalid.', 1;
                IF EXISTS (SELECT 1 FROM dbo.payments WHERE amount <= 0)
                    THROW 51000, 'Schema hardening blocked: payment amount must be positive.', 1;
                IF EXISTS (SELECT 1 FROM dbo.attendance WHERE check_in_time IS NOT NULL AND check_out_time IS NOT NULL AND check_in_time > check_out_time)
                    THROW 51000, 'Schema hardening blocked: attendance checkout precedes check-in.', 1;
                IF EXISTS (SELECT 1 FROM dbo.center_checkins WHERE check_out_time IS NOT NULL AND check_in_time > check_out_time)
                    THROW 51000, 'Schema hardening blocked: center checkout precedes check-in.', 1;
                IF EXISTS (SELECT 1 FROM dbo.training_plans WHERE end_date IS NOT NULL AND end_date < start_date)
                    THROW 51000, 'Schema hardening blocked: training plan end date precedes start date.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_user_notifications_notification_id",
                schema: "dbo",
                table: "user_notifications");

            migrationBuilder.DropIndex(
                name: "IX_payments_invoice_id",
                schema: "dbo",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_center_checkins_center_id",
                schema: "dbo",
                table: "center_checkins");

            migrationBuilder.DropIndex(
                name: "IX_attendance_session_id",
                schema: "dbo",
                table: "attendance");

            migrationBuilder.AlterColumn<long>(
                name: "member_id",
                schema: "dbo",
                table: "training_plans",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<DateTime>(
                name: "paid_at",
                schema: "dbo",
                table: "payments",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "dbo",
                table: "payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql("UPDATE dbo.payments SET created_at = COALESCE(paid_at, SYSUTCDATETIME()) WHERE created_at IS NULL;");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                schema: "dbo",
                table: "payments",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                schema: "dbo",
                table: "payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payment_refunds",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    payment_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    requested_by = table.Column<long>(type: "bigint", nullable: true),
                    approved_by = table.Column<long>(type: "bigint", nullable: true),
                    transaction_code = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    processed_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_refunds", x => x.Id);
                    table.CheckConstraint("CK_payment_refunds_amount_positive", "[amount] > 0");
                    table.ForeignKey(
                        name: "FK_payment_refunds_payments_payment_id",
                        column: x => x.payment_id,
                        principalSchema: "dbo",
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_refunds_users_approved_by",
                        column: x => x.approved_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_refunds_users_requested_by",
                        column: x => x.requested_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_notification_id_user_id",
                schema: "dbo",
                table: "user_notifications",
                columns: new[] { "notification_id", "user_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_training_plans_date_range",
                schema: "dbo",
                table: "training_plans",
                sql: "[end_date] IS NULL OR [start_date] <= [end_date]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_training_plans_target_required",
                schema: "dbo",
                table: "training_plans",
                sql: "[member_id] IS NOT NULL OR [class_id] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_rooms_capacity_positive",
                schema: "dbo",
                table: "rooms",
                sql: "[capacity] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_payments_invoice_id_idempotency_key",
                schema: "dbo",
                table: "payments",
                columns: new[] { "invoice_id", "idempotency_key" },
                unique: true,
                filter: "[idempotency_key] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_payments_amount_positive",
                schema: "dbo",
                table: "payments",
                sql: "[amount] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_membership_packages_duration_positive",
                schema: "dbo",
                table: "membership_packages",
                sql: "[duration_days] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_membership_packages_max_classes_positive",
                schema: "dbo",
                table: "membership_packages",
                sql: "[max_classes] IS NULL OR [max_classes] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_membership_packages_price_nonnegative",
                schema: "dbo",
                table: "membership_packages",
                sql: "[price] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_member_subscriptions_date_range",
                schema: "dbo",
                table: "member_subscriptions",
                sql: "([start_date] IS NULL AND [end_date] IS NULL) OR ([start_date] IS NOT NULL AND [end_date] IS NOT NULL AND [start_date] <= [end_date])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_member_subscriptions_duration_positive",
                schema: "dbo",
                table: "member_subscriptions",
                sql: "[duration_days] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_member_subscriptions_price_nonnegative",
                schema: "dbo",
                table: "member_subscriptions",
                sql: "[price] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_invoices_amounts_nonnegative",
                schema: "dbo",
                table: "invoices",
                sql: "[subtotal] >= 0 AND [discount] >= 0 AND [tax] >= 0 AND [total_amount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_invoices_discount_within_subtotal",
                schema: "dbo",
                table: "invoices",
                sql: "[discount] <= [subtotal]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_invoices_total_matches_components",
                schema: "dbo",
                table: "invoices",
                sql: "[total_amount] = [subtotal] - [discount] + [tax]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_invoice_items_amounts_nonnegative",
                schema: "dbo",
                table: "invoice_items",
                sql: "[unit_price] >= 0 AND [amount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_invoice_items_quantity_positive",
                schema: "dbo",
                table: "invoice_items",
                sql: "[quantity] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_classes_capacity_positive",
                schema: "dbo",
                table: "classes",
                sql: "[capacity] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_classes_duration_positive",
                schema: "dbo",
                table: "classes",
                sql: "[duration_minutes] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_class_sessions_time_range",
                schema: "dbo",
                table: "class_sessions",
                sql: "[start_time] < [end_time]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_class_schedules_date_range",
                schema: "dbo",
                table: "class_schedules",
                sql: "[start_date] IS NULL OR [end_date] IS NULL OR [start_date] <= [end_date]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_class_schedules_day_of_week",
                schema: "dbo",
                table: "class_schedules",
                sql: "[day_of_week] BETWEEN 0 AND 6");

            migrationBuilder.AddCheckConstraint(
                name: "CK_class_schedules_time_range",
                schema: "dbo",
                table: "class_schedules",
                sql: "[start_time] < [end_time]");

            migrationBuilder.CreateIndex(
                name: "IX_class_coaches_class_id",
                schema: "dbo",
                table: "class_coaches",
                column: "class_id",
                unique: true,
                filter: "[is_primary] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_center_checkins_center_id_member_id",
                schema: "dbo",
                table: "center_checkins",
                columns: new[] { "center_id", "member_id" },
                unique: true,
                filter: "[check_out_time] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_center_checkins_time_range",
                schema: "dbo",
                table: "center_checkins",
                sql: "[check_out_time] IS NULL OR [check_in_time] <= [check_out_time]");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_session_id_member_id",
                schema: "dbo",
                table: "attendance",
                columns: new[] { "session_id", "member_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_attendance_time_range",
                schema: "dbo",
                table: "attendance",
                sql: "[check_out_time] IS NULL OR [check_in_time] IS NULL OR [check_in_time] <= [check_out_time]");

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_approved_by",
                schema: "dbo",
                table: "payment_refunds",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_payment_id",
                schema: "dbo",
                table: "payment_refunds",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_requested_by",
                schema: "dbo",
                table: "payment_refunds",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_transaction_code",
                schema: "dbo",
                table: "payment_refunds",
                column: "transaction_code",
                unique: true,
                filter: "[transaction_code] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_refunds",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_user_notifications_notification_id_user_id",
                schema: "dbo",
                table: "user_notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_training_plans_date_range",
                schema: "dbo",
                table: "training_plans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_training_plans_target_required",
                schema: "dbo",
                table: "training_plans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_rooms_capacity_positive",
                schema: "dbo",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "IX_payments_invoice_id_idempotency_key",
                schema: "dbo",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_payments_amount_positive",
                schema: "dbo",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_membership_packages_duration_positive",
                schema: "dbo",
                table: "membership_packages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_membership_packages_max_classes_positive",
                schema: "dbo",
                table: "membership_packages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_membership_packages_price_nonnegative",
                schema: "dbo",
                table: "membership_packages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_member_subscriptions_date_range",
                schema: "dbo",
                table: "member_subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_member_subscriptions_duration_positive",
                schema: "dbo",
                table: "member_subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_member_subscriptions_price_nonnegative",
                schema: "dbo",
                table: "member_subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_invoices_amounts_nonnegative",
                schema: "dbo",
                table: "invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_invoices_discount_within_subtotal",
                schema: "dbo",
                table: "invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_invoices_total_matches_components",
                schema: "dbo",
                table: "invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_invoice_items_amounts_nonnegative",
                schema: "dbo",
                table: "invoice_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_invoice_items_quantity_positive",
                schema: "dbo",
                table: "invoice_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_classes_capacity_positive",
                schema: "dbo",
                table: "classes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_classes_duration_positive",
                schema: "dbo",
                table: "classes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_class_sessions_time_range",
                schema: "dbo",
                table: "class_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_class_schedules_date_range",
                schema: "dbo",
                table: "class_schedules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_class_schedules_day_of_week",
                schema: "dbo",
                table: "class_schedules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_class_schedules_time_range",
                schema: "dbo",
                table: "class_schedules");

            migrationBuilder.DropIndex(
                name: "IX_class_coaches_class_id",
                schema: "dbo",
                table: "class_coaches");

            migrationBuilder.DropIndex(
                name: "IX_center_checkins_center_id_member_id",
                schema: "dbo",
                table: "center_checkins");

            migrationBuilder.DropCheckConstraint(
                name: "CK_center_checkins_time_range",
                schema: "dbo",
                table: "center_checkins");

            migrationBuilder.DropIndex(
                name: "IX_attendance_session_id_member_id",
                schema: "dbo",
                table: "attendance");

            migrationBuilder.DropCheckConstraint(
                name: "CK_attendance_time_range",
                schema: "dbo",
                table: "attendance");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "dbo",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                schema: "dbo",
                table: "payments");

            migrationBuilder.AlterColumn<long>(
                name: "member_id",
                schema: "dbo",
                table: "training_plans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "paid_at",
                schema: "dbo",
                table: "payments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_notification_id",
                schema: "dbo",
                table: "user_notifications",
                column: "notification_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_invoice_id",
                schema: "dbo",
                table: "payments",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_center_checkins_center_id",
                schema: "dbo",
                table: "center_checkins",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_session_id",
                schema: "dbo",
                table: "attendance",
                column: "session_id");
        }
    }
}
