using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachProfessionalWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "center_id",
                schema: "dbo",
                table: "exercises",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "created_by_coach_id",
                schema: "dbo",
                table: "exercises",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_url",
                schema: "dbo",
                table: "exercises",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "leave_type",
                schema: "dbo",
                table: "coach_leaves",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "AnnualLeave");

            migrationBuilder.AddColumn<string>(
                name: "manager_note",
                schema: "dbo",
                table: "coach_leaves",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reason",
                schema: "dbo",
                table: "coach_leaves",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "requested_at",
                schema: "dbo",
                table: "coach_leaves",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<DateTime>(
                name: "reviewed_at",
                schema: "dbo",
                table: "coach_leaves",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "reviewed_by",
                schema: "dbo",
                table: "coach_leaves",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "swap_coach_id",
                schema: "dbo",
                table: "coach_leaves",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "coach_compensation_policies",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    monthly_base_salary = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    commission_per_session = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    sales_commission_percent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    target_sessions = table.Column<int>(type: "int", nullable: false),
                    target_sales = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    updated_by = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coach_compensation_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_coach_compensation_policies_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_compensation_policies_users_updated_by",
                        column: x => x.updated_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coach_messages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    sender_user_id = table.Column<long>(type: "bigint", nullable: false),
                    content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    read_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coach_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_coach_messages_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_messages_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_messages_users_sender_user_id",
                        column: x => x.sender_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coach_sales_credits",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    member_id = table.Column<long>(type: "bigint", nullable: true),
                    subscription_id = table.Column<long>(type: "bigint", nullable: true),
                    amount = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    credited_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coach_sales_credits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_coach_sales_credits_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_sales_credits_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_sales_credits_member_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalSchema: "dbo",
                        principalTable: "member_subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_sales_credits_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coach_work_events",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    center_id = table.Column<long>(type: "bigint", nullable: false),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    member_id = table.Column<long>(type: "bigint", nullable: true),
                    room_id = table.Column<long>(type: "bigint", nullable: true),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    event_type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    start_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    end_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coach_work_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_coach_work_events_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_work_events_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_work_events_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_work_events_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "dbo",
                        principalTable: "rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_work_events_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "communication_deliveries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    notification_id = table.Column<long>(type: "bigint", nullable: true),
                    recipient_user_id = table.Column<long>(type: "bigint", nullable: false),
                    channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    provider_message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    sent_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communication_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_communication_deliveries_notifications_notification_id",
                        column: x => x.notification_id,
                        principalSchema: "dbo",
                        principalTable: "notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_communication_deliveries_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "facility_resources",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    center_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    resource_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    capacity = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facility_resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_facility_resources_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "food_diary_entries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    entry_date = table.Column<DateOnly>(type: "date", nullable: false),
                    meal_type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    food_name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    calories = table.Column<int>(type: "int", nullable: true),
                    protein_grams = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    carb_grams = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    fat_grams = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_diary_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_food_diary_entries_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "member_health_records",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    record_type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    details = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    occurred_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_health_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_member_health_records_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_member_health_records_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "nutrition_plans",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    daily_calories = table.Column<int>(type: "int", nullable: false),
                    protein_grams = table.Column<int>(type: "int", nullable: false),
                    carb_grams = table.Column<int>(type: "int", nullable: false),
                    fat_grams = table.Column<int>(type: "int", nullable: false),
                    meal_plan_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrition_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_nutrition_plans_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_nutrition_plans_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coach_reviews",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    work_event_id = table.Column<long>(type: "bigint", nullable: true),
                    rating = table.Column<int>(type: "int", nullable: false),
                    comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coach_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_coach_reviews_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_reviews_coach_work_events_work_event_id",
                        column: x => x.work_event_id,
                        principalSchema: "dbo",
                        principalTable: "coach_work_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_reviews_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "equipment_issues",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    center_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_id = table.Column<long>(type: "bigint", nullable: true),
                    reported_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    photo_url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    resolution_note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    resolved_by = table.Column<long>(type: "bigint", nullable: true),
                    reported_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_equipment_issues_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_equipment_issues_facility_resources_resource_id",
                        column: x => x.resource_id,
                        principalSchema: "dbo",
                        principalTable: "facility_resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_equipment_issues_users_reported_by_user_id",
                        column: x => x.reported_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_equipment_issues_users_resolved_by",
                        column: x => x.resolved_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resource_bookings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    resource_id = table.Column<long>(type: "bigint", nullable: false),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    work_event_id = table.Column<long>(type: "bigint", nullable: true),
                    start_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    end_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    purpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_resource_bookings_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_resource_bookings_coach_work_events_work_event_id",
                        column: x => x.work_event_id,
                        principalSchema: "dbo",
                        principalTable: "coach_work_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_resource_bookings_facility_resources_resource_id",
                        column: x => x.resource_id,
                        principalSchema: "dbo",
                        principalTable: "facility_resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exercises_center_id",
                schema: "dbo",
                table: "exercises",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "IX_exercises_created_by_coach_id",
                schema: "dbo",
                table: "exercises",
                column: "created_by_coach_id");

            migrationBuilder.CreateIndex(
                name: "IX_coach_leaves_reviewed_by",
                schema: "dbo",
                table: "coach_leaves",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_coach_leaves_swap_coach_id",
                schema: "dbo",
                table: "coach_leaves",
                column: "swap_coach_id");

            migrationBuilder.CreateIndex(
                name: "IX_coach_compensation_policies_coach_id_effective_from",
                schema: "dbo",
                table: "coach_compensation_policies",
                columns: new[] { "coach_id", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "IX_coach_compensation_policies_updated_by",
                schema: "dbo",
                table: "coach_compensation_policies",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "IX_coach_messages_coach_id_member_id_created_at",
                schema: "dbo",
                table: "coach_messages",
                columns: new[] { "coach_id", "member_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_coach_messages_member_id",
                schema: "dbo",
                table: "coach_messages",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_coach_messages_sender_user_id",
                schema: "dbo",
                table: "coach_messages",
                column: "sender_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_coach_reviews_coach_id_member_id_work_event_id",
                schema: "dbo",
                table: "coach_reviews",
                columns: new[] { "coach_id", "member_id", "work_event_id" },
                unique: true,
                filter: "[work_event_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_coach_reviews_member_id",
                schema: "dbo",
                table: "coach_reviews",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_coach_reviews_work_event_id",
                schema: "dbo",
                table: "coach_reviews",
                column: "work_event_id");

            migrationBuilder.CreateIndex(
                name: "IX_coach_sales_credits_coach_id",
                schema: "dbo",
                table: "coach_sales_credits",
                column: "coach_id");

            migrationBuilder.CreateIndex(
                name: "IX_coach_sales_credits_created_by",
                schema: "dbo",
                table: "coach_sales_credits",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_coach_sales_credits_member_id",
                schema: "dbo",
                table: "coach_sales_credits",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_coach_sales_credits_subscription_id",
                schema: "dbo",
                table: "coach_sales_credits",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_coach_work_events_center_id",
                schema: "dbo",
                table: "coach_work_events",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "IX_coach_work_events_coach_id_start_at_end_at",
                schema: "dbo",
                table: "coach_work_events",
                columns: new[] { "coach_id", "start_at", "end_at" });

            migrationBuilder.CreateIndex(
                name: "IX_coach_work_events_created_by",
                schema: "dbo",
                table: "coach_work_events",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_coach_work_events_member_id",
                schema: "dbo",
                table: "coach_work_events",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_coach_work_events_room_id",
                schema: "dbo",
                table: "coach_work_events",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "IX_communication_deliveries_notification_id",
                schema: "dbo",
                table: "communication_deliveries",
                column: "notification_id");

            migrationBuilder.CreateIndex(
                name: "IX_communication_deliveries_recipient_user_id",
                schema: "dbo",
                table: "communication_deliveries",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_communication_deliveries_status_created_at",
                schema: "dbo",
                table: "communication_deliveries",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_equipment_issues_center_id_status_reported_at",
                schema: "dbo",
                table: "equipment_issues",
                columns: new[] { "center_id", "status", "reported_at" });

            migrationBuilder.CreateIndex(
                name: "IX_equipment_issues_reported_by_user_id",
                schema: "dbo",
                table: "equipment_issues",
                column: "reported_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_equipment_issues_resolved_by",
                schema: "dbo",
                table: "equipment_issues",
                column: "resolved_by");

            migrationBuilder.CreateIndex(
                name: "IX_equipment_issues_resource_id",
                schema: "dbo",
                table: "equipment_issues",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "IX_facility_resources_center_id_name",
                schema: "dbo",
                table: "facility_resources",
                columns: new[] { "center_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_food_diary_entries_member_id_entry_date",
                schema: "dbo",
                table: "food_diary_entries",
                columns: new[] { "member_id", "entry_date" });

            migrationBuilder.CreateIndex(
                name: "IX_member_health_records_coach_id",
                schema: "dbo",
                table: "member_health_records",
                column: "coach_id");

            migrationBuilder.CreateIndex(
                name: "IX_member_health_records_member_id_occurred_on",
                schema: "dbo",
                table: "member_health_records",
                columns: new[] { "member_id", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "IX_nutrition_plans_coach_id",
                schema: "dbo",
                table: "nutrition_plans",
                column: "coach_id");

            migrationBuilder.CreateIndex(
                name: "IX_nutrition_plans_member_id_status",
                schema: "dbo",
                table: "nutrition_plans",
                columns: new[] { "member_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_resource_bookings_coach_id",
                schema: "dbo",
                table: "resource_bookings",
                column: "coach_id");

            migrationBuilder.CreateIndex(
                name: "IX_resource_bookings_resource_id_start_at_end_at",
                schema: "dbo",
                table: "resource_bookings",
                columns: new[] { "resource_id", "start_at", "end_at" });

            migrationBuilder.CreateIndex(
                name: "IX_resource_bookings_work_event_id",
                schema: "dbo",
                table: "resource_bookings",
                column: "work_event_id");

            migrationBuilder.AddForeignKey(
                name: "FK_coach_leaves_coach_profiles_swap_coach_id",
                schema: "dbo",
                table: "coach_leaves",
                column: "swap_coach_id",
                principalSchema: "dbo",
                principalTable: "coach_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_coach_leaves_users_reviewed_by",
                schema: "dbo",
                table: "coach_leaves",
                column: "reviewed_by",
                principalSchema: "dbo",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_exercises_centers_center_id",
                schema: "dbo",
                table: "exercises",
                column: "center_id",
                principalSchema: "dbo",
                principalTable: "centers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_exercises_coach_profiles_created_by_coach_id",
                schema: "dbo",
                table: "exercises",
                column: "created_by_coach_id",
                principalSchema: "dbo",
                principalTable: "coach_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_coach_leaves_coach_profiles_swap_coach_id",
                schema: "dbo",
                table: "coach_leaves");

            migrationBuilder.DropForeignKey(
                name: "FK_coach_leaves_users_reviewed_by",
                schema: "dbo",
                table: "coach_leaves");

            migrationBuilder.DropForeignKey(
                name: "FK_exercises_centers_center_id",
                schema: "dbo",
                table: "exercises");

            migrationBuilder.DropForeignKey(
                name: "FK_exercises_coach_profiles_created_by_coach_id",
                schema: "dbo",
                table: "exercises");

            migrationBuilder.DropTable(
                name: "coach_compensation_policies",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "coach_messages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "coach_reviews",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "coach_sales_credits",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "communication_deliveries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "equipment_issues",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "food_diary_entries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "member_health_records",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "nutrition_plans",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "resource_bookings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "coach_work_events",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "facility_resources",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_exercises_center_id",
                schema: "dbo",
                table: "exercises");

            migrationBuilder.DropIndex(
                name: "IX_exercises_created_by_coach_id",
                schema: "dbo",
                table: "exercises");

            migrationBuilder.DropIndex(
                name: "IX_coach_leaves_reviewed_by",
                schema: "dbo",
                table: "coach_leaves");

            migrationBuilder.DropIndex(
                name: "IX_coach_leaves_swap_coach_id",
                schema: "dbo",
                table: "coach_leaves");

            migrationBuilder.DropColumn(
                name: "center_id",
                schema: "dbo",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "created_by_coach_id",
                schema: "dbo",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "image_url",
                schema: "dbo",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "leave_type",
                schema: "dbo",
                table: "coach_leaves");

            migrationBuilder.DropColumn(
                name: "manager_note",
                schema: "dbo",
                table: "coach_leaves");

            migrationBuilder.DropColumn(
                name: "reason",
                schema: "dbo",
                table: "coach_leaves");

            migrationBuilder.DropColumn(
                name: "requested_at",
                schema: "dbo",
                table: "coach_leaves");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                schema: "dbo",
                table: "coach_leaves");

            migrationBuilder.DropColumn(
                name: "reviewed_by",
                schema: "dbo",
                table: "coach_leaves");

            migrationBuilder.DropColumn(
                name: "swap_coach_id",
                schema: "dbo",
                table: "coach_leaves");
        }
    }
}
