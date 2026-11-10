using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "centers",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    address = table.Column<string>(type: "citext", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "citext", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "citext", maxLength: 150, nullable: true),
                    description = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    opening_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    closing_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_centers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_verification_tokens",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    token = table.Column<string>(type: "citext", maxLength: 255, nullable: false),
                    failed_attempts = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_verification_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "citext", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "citext", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "citext", maxLength: 255, nullable: true),
                    module = table.Column<string>(type: "citext", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "citext", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "citext", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "uploaded_media",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    drive_file_id = table.Column<string>(type: "text", maxLength: 255, nullable: false),
                    file_name = table.Column<string>(type: "citext", maxLength: 500, nullable: false),
                    mime_type = table.Column<string>(type: "citext", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: true),
                    file_url = table.Column<string>(type: "citext", maxLength: 1000, nullable: false),
                    web_view_link = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    uploaded_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    center_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uploaded_media", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "facility_resources",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    center_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    resource_type = table.Column<string>(type: "citext", maxLength: 50, nullable: false),
                    location = table.Column<string>(type: "citext", maxLength: 300, nullable: true),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facility_resources", x => x.id);
                    table.ForeignKey(
                        name: "FK_facility_resources_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "membership_packages",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    center_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    max_classes = table.Column<int>(type: "integer", nullable: true),
                    access_type = table.Column<string>(type: "citext", maxLength: 50, nullable: true),
                    allowed_sports = table.Column<int>(type: "integer", nullable: false),
                    badge = table.Column<string>(type: "citext", maxLength: 50, nullable: true),
                    features = table.Column<string>(type: "citext", nullable: true),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership_packages", x => x.id);
                    table.ForeignKey(
                        name: "FK_membership_packages_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rooms",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    center_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "citext", maxLength: 100, nullable: false),
                    room_type = table.Column<string>(type: "citext", maxLength: 100, nullable: true),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    location_description = table.Column<string>(type: "citext", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rooms", x => x.id);
                    table.ForeignKey(
                        name: "FK_rooms_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sports",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    center_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "citext", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sports", x => x.id);
                    table.ForeignKey(
                        name: "FK_sports_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "dbo",
                columns: table => new
                {
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    permission_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "dbo",
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "dbo",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "citext", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    google_subject = table.Column<string>(type: "citext", maxLength: 255, nullable: true),
                    password_hash = table.Column<string>(type: "citext", maxLength: 255, nullable: false),
                    auth_version = table.Column<int>(type: "integer", nullable: false),
                    phone = table.Column<string>(type: "citext", maxLength: 20, nullable: true),
                    avatar_key = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    failed_login_attempts = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    is_email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "dbo",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "classes",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    center_id = table.Column<long>(type: "bigint", nullable: false),
                    sport_id = table.Column<long>(type: "bigint", nullable: false),
                    room_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    level = table.Column<string>(type: "citext", maxLength: 50, nullable: true),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    allow_waitlist = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_classes", x => x.id);
                    table.ForeignKey(
                        name: "FK_classes_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_classes_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "dbo",
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_classes_sports_sport_id",
                        column: x => x.sport_id,
                        principalSchema: "dbo",
                        principalTable: "sports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "account_activation_tokens",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    token_hash = table.Column<string>(type: "citext", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_activation_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_activation_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: true),
                    center_id = table.Column<long>(type: "bigint", nullable: true),
                    action = table.Column<string>(type: "citext", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "citext", maxLength: 100, nullable: false),
                    entity_id = table.Column<long>(type: "bigint", nullable: true),
                    old_values = table.Column<string>(type: "citext", nullable: true),
                    new_values = table.Column<string>(type: "citext", nullable: true),
                    ip_address = table.Column<string>(type: "citext", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_logs_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coach_profiles",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    center_id = table.Column<long>(type: "bigint", nullable: false),
                    coach_code = table.Column<string>(type: "citext", maxLength: 50, nullable: false),
                    full_name = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    specialization = table.Column<string>(type: "citext", maxLength: 255, nullable: true),
                    certification = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    experience_years = table.Column<int>(type: "integer", nullable: true),
                    bio = table.Column<string>(type: "citext", nullable: true),
                    hire_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coach_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_coach_profiles_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_profiles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "equipment_issues",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    center_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_id = table.Column<long>(type: "bigint", nullable: true),
                    reported_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "citext", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "citext", nullable: false),
                    severity = table.Column<string>(type: "citext", maxLength: 20, nullable: false),
                    photo_url = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    resolution_note = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    resolved_by = table.Column<long>(type: "bigint", nullable: true),
                    reported_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_issues", x => x.id);
                    table.ForeignKey(
                        name: "FK_equipment_issues_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_equipment_issues_facility_resources_resource_id",
                        column: x => x.resource_id,
                        principalSchema: "dbo",
                        principalTable: "facility_resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_equipment_issues_users_reported_by_user_id",
                        column: x => x.reported_by_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_equipment_issues_users_resolved_by",
                        column: x => x.resolved_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "member_profiles",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    center_id = table.Column<long>(type: "bigint", nullable: true),
                    member_code = table.Column<string>(type: "citext", maxLength: 50, nullable: false),
                    full_name = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    gender = table.Column<string>(type: "citext", maxLength: 20, nullable: true),
                    address = table.Column<string>(type: "citext", maxLength: 255, nullable: true),
                    emergency_contact_name = table.Column<string>(type: "citext", maxLength: 150, nullable: true),
                    emergency_contact_phone = table.Column<string>(type: "citext", maxLength: 20, nullable: true),
                    fitness_goal = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    fitness_level = table.Column<string>(type: "citext", maxLength: 50, nullable: true),
                    medical_note = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    height_cm = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    weight_kg = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    booking_suspended_until = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_member_profiles_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_member_profiles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sender_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "citext", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "citext", nullable: false),
                    notification_type = table.Column<string>(type: "citext", maxLength: 50, nullable: false),
                    target_class_ids = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    scheduled_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    reminder_key = table.Column<string>(type: "citext", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_users_sender_id",
                        column: x => x.sender_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    token = table.Column<string>(type: "citext", maxLength: 255, nullable: false),
                    failed_attempts = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_password_reset_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_compensation_policies",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    center_id = table.Column<long>(type: "bigint", nullable: true),
                    monthly_base_salary = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    monthly_allowance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    updated_by = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_compensation_policies", x => x.id);
                    table.ForeignKey(
                        name: "FK_staff_compensation_policies_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_compensation_policies_users_updated_by",
                        column: x => x.updated_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_compensation_policies_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_profiles",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    center_id = table.Column<long>(type: "bigint", nullable: false),
                    staff_code = table.Column<string>(type: "citext", maxLength: 50, nullable: false),
                    full_name = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    position = table.Column<string>(type: "citext", maxLength: 100, nullable: false),
                    hire_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_staff_profiles_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_profiles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    setting_key = table.Column<string>(type: "citext", maxLength: 100, nullable: false),
                    setting_value = table.Column<string>(type: "citext", nullable: true),
                    description = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_system_settings_users_updated_by",
                        column: x => x.updated_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "class_schedules",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    class_id = table.Column<long>(type: "bigint", nullable: false),
                    room_id = table.Column<long>(type: "bigint", nullable: true),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_schedules", x => x.id);
                    table.ForeignKey(
                        name: "FK_class_schedules_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "dbo",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_schedules_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "dbo",
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "class_coaches",
                schema: "dbo",
                columns: table => new
                {
                    class_id = table.Column<long>(type: "bigint", nullable: false),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    assigned_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_coaches", x => new { x.class_id, x.coach_id });
                    table.ForeignKey(
                        name: "FK_class_coaches_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "dbo",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_coaches_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coach_compensation_policies",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    monthly_base_salary = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    commission_per_session = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    sales_commission_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    target_sessions = table.Column<int>(type: "integer", nullable: false),
                    target_sales = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    updated_by = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coach_compensation_policies", x => x.id);
                    table.ForeignKey(
                        name: "FK_coach_compensation_policies_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_compensation_policies_users_updated_by",
                        column: x => x.updated_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coach_leaves",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    start_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    end_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    leave_type = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    reason = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    swap_coach_id = table.Column<long>(type: "bigint", nullable: true),
                    manager_note = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    reviewed_by = table.Column<long>(type: "bigint", nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coach_leaves", x => x.id);
                    table.ForeignKey(
                        name: "FK_coach_leaves_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_leaves_coach_profiles_swap_coach_id",
                        column: x => x.swap_coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_leaves_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coach_sports",
                schema: "dbo",
                columns: table => new
                {
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    sport_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coach_sports", x => new { x.coach_id, x.sport_id });
                    table.ForeignKey(
                        name: "FK_coach_sports_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_coach_sports_sports_sport_id",
                        column: x => x.sport_id,
                        principalSchema: "dbo",
                        principalTable: "sports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exercises",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    center_id = table.Column<long>(type: "bigint", nullable: true),
                    created_by_coach_id = table.Column<long>(type: "bigint", nullable: true),
                    sport_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    muscle_group = table.Column<string>(type: "citext", maxLength: 100, nullable: true),
                    difficulty_level = table.Column<string>(type: "citext", maxLength: 50, nullable: true),
                    instructions = table.Column<string>(type: "citext", nullable: true),
                    video_url = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    image_url = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercises", x => x.id);
                    table.ForeignKey(
                        name: "FK_exercises_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exercises_coach_profiles_created_by_coach_id",
                        column: x => x.created_by_coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exercises_sports_sport_id",
                        column: x => x.sport_id,
                        principalSchema: "dbo",
                        principalTable: "sports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_conversations",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    member_id = table.Column<long>(type: "bigint", nullable: true),
                    title = table.Column<string>(type: "citext", maxLength: 200, nullable: false),
                    conversation_type = table.Column<string>(type: "citext", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_conversations", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_conversations_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_conversations_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assignments",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    coach_id = table.Column<long>(type: "bigint", nullable: true),
                    class_id = table.Column<long>(type: "bigint", nullable: true),
                    member_id = table.Column<long>(type: "bigint", nullable: true),
                    title = table.Column<string>(type: "citext", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "citext", nullable: true),
                    due_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_assignments_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "dbo",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assignments_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assignments_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "center_checkins",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    center_id = table.Column<long>(type: "bigint", nullable: false),
                    checked_in_by = table.Column<long>(type: "bigint", nullable: true),
                    check_in_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    check_out_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_center_checkins", x => x.id);
                    table.ForeignKey(
                        name: "FK_center_checkins_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_center_checkins_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_center_checkins_users_checked_in_by",
                        column: x => x.checked_in_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coach_messages",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    sender_user_id = table.Column<long>(type: "bigint", nullable: false),
                    content = table.Column<string>(type: "citext", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coach_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_coach_messages_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_messages_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_messages_users_sender_user_id",
                        column: x => x.sender_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coach_work_events",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    center_id = table.Column<long>(type: "bigint", nullable: false),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    member_id = table.Column<long>(type: "bigint", nullable: true),
                    room_id = table.Column<long>(type: "bigint", nullable: true),
                    title = table.Column<string>(type: "citext", maxLength: 200, nullable: false),
                    event_type = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    start_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    end_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    notes = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coach_work_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_coach_work_events_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_work_events_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_work_events_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_work_events_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "dbo",
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_work_events_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "food_diary_entries",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    entry_date = table.Column<DateOnly>(type: "date", nullable: false),
                    meal_type = table.Column<string>(type: "citext", maxLength: 40, nullable: false),
                    food_name = table.Column<string>(type: "citext", maxLength: 300, nullable: false),
                    calories = table.Column<int>(type: "integer", nullable: true),
                    protein_grams = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    carb_grams = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    fat_grams = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_diary_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_food_diary_entries_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    invoice_number = table.Column<string>(type: "citext", maxLength: 50, nullable: false),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    center_id = table.Column<long>(type: "bigint", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    discount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    tax = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    idempotency_key = table.Column<string>(type: "citext", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoices_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "member_health_records",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    record_type = table.Column<string>(type: "citext", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "citext", maxLength: 200, nullable: false),
                    details = table.Column<string>(type: "citext", nullable: false),
                    occurred_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_health_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_member_health_records_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_member_health_records_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "member_subscriptions",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    package_id = table.Column<long>(type: "bigint", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    auto_renew = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_member_subscriptions_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_member_subscriptions_membership_packages_package_id",
                        column: x => x.package_id,
                        principalSchema: "dbo",
                        principalTable: "membership_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "nutrition_plans",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "citext", maxLength: 200, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    daily_calories = table.Column<int>(type: "integer", nullable: false),
                    protein_grams = table.Column<int>(type: "integer", nullable: false),
                    carb_grams = table.Column<int>(type: "integer", nullable: false),
                    fat_grams = table.Column<int>(type: "integer", nullable: false),
                    meal_plan_json = table.Column<string>(type: "citext", nullable: true),
                    notes = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrition_plans", x => x.id);
                    table.ForeignKey(
                        name: "FK_nutrition_plans_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_nutrition_plans_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "support_requests",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    member_id = table.Column<long>(type: "bigint", nullable: true),
                    requester_id = table.Column<long>(type: "bigint", nullable: false),
                    center_id = table.Column<long>(type: "bigint", nullable: false),
                    handled_by = table.Column<long>(type: "bigint", nullable: true),
                    subject = table.Column<string>(type: "citext", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "citext", nullable: false),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    priority = table.Column<string>(type: "citext", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_support_requests_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_support_requests_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_support_requests_users_handled_by",
                        column: x => x.handled_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_support_requests_users_requester_id",
                        column: x => x.requester_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "training_plans",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    member_id = table.Column<long>(type: "bigint", nullable: true),
                    exercises_json = table.Column<string>(type: "citext", nullable: true),
                    fitness_condition = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    class_id = table.Column<long>(type: "bigint", nullable: true),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    goal = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    plan_type = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_plans", x => x.id);
                    table.ForeignKey(
                        name: "FK_training_plans_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "dbo",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_training_plans_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_training_plans_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "communication_deliveries",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    notification_id = table.Column<long>(type: "bigint", nullable: true),
                    recipient_user_id = table.Column<long>(type: "bigint", nullable: false),
                    channel = table.Column<string>(type: "citext", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    provider_message = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    lease_until = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    locked_by = table.Column<string>(type: "citext", maxLength: 100, nullable: true),
                    last_error = table.Column<string>(type: "citext", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communication_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_communication_deliveries_notifications_notification_id",
                        column: x => x.notification_id,
                        principalSchema: "dbo",
                        principalTable: "notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_communication_deliveries_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_notifications",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    notification_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_notifications_notifications_notification_id",
                        column: x => x.notification_id,
                        principalSchema: "dbo",
                        principalTable: "notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_notifications_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "class_sessions",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    class_id = table.Column<long>(type: "bigint", nullable: false),
                    schedule_id = table.Column<long>(type: "bigint", nullable: true),
                    room_id = table.Column<long>(type: "bigint", nullable: true),
                    coach_id = table.Column<long>(type: "bigint", nullable: true),
                    session_date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    session_status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    notes = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_class_sessions_class_schedules_schedule_id",
                        column: x => x.schedule_id,
                        principalSchema: "dbo",
                        principalTable: "class_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_sessions_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "dbo",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_sessions_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_sessions_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "dbo",
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_messages",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conversation_id = table.Column<long>(type: "bigint", nullable: false),
                    sender_type = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    message = table.Column<string>(type: "citext", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_messages_ai_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalSchema: "dbo",
                        principalTable: "ai_conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assignment_submissions",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    assignment_id = table.Column<long>(type: "bigint", nullable: false),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    content = table.Column<string>(type: "citext", nullable: true),
                    score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    coach_feedback = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignment_submissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_assignment_submissions_assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalSchema: "dbo",
                        principalTable: "assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assignment_submissions_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coach_reviews",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    work_event_id = table.Column<long>(type: "bigint", nullable: true),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "citext", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coach_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_coach_reviews_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_reviews_coach_work_events_work_event_id",
                        column: x => x.work_event_id,
                        principalSchema: "dbo",
                        principalTable: "coach_work_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_reviews_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resource_bookings",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    resource_id = table.Column<long>(type: "bigint", nullable: false),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    work_event_id = table.Column<long>(type: "bigint", nullable: true),
                    start_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    end_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    purpose = table.Column<string>(type: "citext", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_bookings", x => x.id);
                    table.ForeignKey(
                        name: "FK_resource_bookings_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_resource_bookings_coach_work_events_work_event_id",
                        column: x => x.work_event_id,
                        principalSchema: "dbo",
                        principalTable: "coach_work_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_resource_bookings_facility_resources_resource_id",
                        column: x => x.resource_id,
                        principalSchema: "dbo",
                        principalTable: "facility_resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    processed_by = table.Column<long>(type: "bigint", nullable: true),
                    payment_method = table.Column<string>(type: "citext", maxLength: 50, nullable: false),
                    transaction_code = table.Column<string>(type: "citext", maxLength: 150, nullable: true),
                    gateway_reference = table.Column<string>(type: "citext", maxLength: 100, nullable: true),
                    provider_transaction_id = table.Column<string>(type: "citext", maxLength: 150, nullable: true),
                    idempotency_key = table.Column<string>(type: "citext", maxLength: 100, nullable: true),
                    gateway_payment_url = table.Column<string>(type: "citext", maxLength: 2048, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    amount_received = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    payment_status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    attempted_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    note = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    refund_approved_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.ForeignKey(
                        name: "FK_payments_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalSchema: "dbo",
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_users_processed_by",
                        column: x => x.processed_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_users_refund_approved_by",
                        column: x => x.refund_approved_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "class_enrollments",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    class_id = table.Column<long>(type: "bigint", nullable: false),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    subscription_id = table.Column<long>(type: "bigint", nullable: true),
                    registered_by = table.Column<long>(type: "bigint", nullable: true),
                    registered_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    cancelled_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    cancellation_reason = table.Column<string>(type: "citext", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_enrollments", x => x.id);
                    table.ForeignKey(
                        name: "FK_class_enrollments_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "dbo",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_enrollments_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_enrollments_member_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalSchema: "dbo",
                        principalTable: "member_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_enrollments_users_registered_by",
                        column: x => x.registered_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coach_sales_credits",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    member_id = table.Column<long>(type: "bigint", nullable: true),
                    subscription_id = table.Column<long>(type: "bigint", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    credited_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    note = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coach_sales_credits", x => x.id);
                    table.ForeignKey(
                        name: "FK_coach_sales_credits_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_sales_credits_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_sales_credits_member_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalSchema: "dbo",
                        principalTable: "member_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coach_sales_credits_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "support_request_messages",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    request_id = table.Column<long>(type: "bigint", nullable: false),
                    sender_id = table.Column<long>(type: "bigint", nullable: false),
                    message = table.Column<string>(type: "citext", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_request_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_support_request_messages_support_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "dbo",
                        principalTable: "support_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_support_request_messages_users_sender_id",
                        column: x => x.sender_id,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_exercise_recommendations",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    conversation_id = table.Column<long>(type: "bigint", nullable: true),
                    exercise_id = table.Column<long>(type: "bigint", nullable: true),
                    training_plan_id = table.Column<long>(type: "bigint", nullable: true),
                    requested_by = table.Column<long>(type: "bigint", nullable: true),
                    recommendation_reason = table.Column<string>(type: "citext", nullable: true),
                    target_goal = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    difficulty_level = table.Column<string>(type: "citext", maxLength: 50, nullable: true),
                    ai_model = table.Column<string>(type: "citext", maxLength: 100, nullable: true),
                    confidence_score = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    accepted = table.Column<bool>(type: "boolean", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_exercise_recommendations", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_exercise_recommendations_ai_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalSchema: "dbo",
                        principalTable: "ai_conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_exercise_recommendations_exercises_exercise_id",
                        column: x => x.exercise_id,
                        principalSchema: "dbo",
                        principalTable: "exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_exercise_recommendations_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_exercise_recommendations_training_plans_training_plan_id",
                        column: x => x.training_plan_id,
                        principalSchema: "dbo",
                        principalTable: "training_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_exercise_recommendations_users_requested_by",
                        column: x => x.requested_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "member_progress_reviews",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    training_plan_id = table.Column<long>(type: "bigint", nullable: true),
                    review_date = table.Column<DateOnly>(type: "date", nullable: false),
                    progress_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    weight = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    body_fat_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    review_note = table.Column<string>(type: "citext", maxLength: 2000, nullable: true),
                    next_goal = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_progress_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_member_progress_reviews_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_member_progress_reviews_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_member_progress_reviews_training_plans_training_plan_id",
                        column: x => x.training_plan_id,
                        principalSchema: "dbo",
                        principalTable: "training_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "training_plan_exercises",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    training_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    exercise_id = table.Column<long>(type: "bigint", nullable: false),
                    day_number = table.Column<int>(type: "integer", nullable: true),
                    sets = table.Column<int>(type: "integer", nullable: true),
                    repetitions = table.Column<int>(type: "integer", nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    rest_seconds = table.Column<int>(type: "integer", nullable: true),
                    target_weight = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_plan_exercises", x => x.id);
                    table.ForeignKey(
                        name: "FK_training_plan_exercises_exercises_exercise_id",
                        column: x => x.exercise_id,
                        principalSchema: "dbo",
                        principalTable: "exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_training_plan_exercises_training_plans_training_plan_id",
                        column: x => x.training_plan_id,
                        principalSchema: "dbo",
                        principalTable: "training_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<long>(type: "bigint", nullable: false),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    checked_in_by = table.Column<long>(type: "bigint", nullable: true),
                    attendance_status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    check_in_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    check_out_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    note = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance", x => x.id);
                    table.ForeignKey(
                        name: "FK_attendance_class_sessions_session_id",
                        column: x => x.session_id,
                        principalSchema: "dbo",
                        principalTable: "class_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attendance_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attendance_users_checked_in_by",
                        column: x => x.checked_in_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "class_waitlist",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<long>(type: "bigint", nullable: true),
                    class_id = table.Column<long>(type: "bigint", nullable: false),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    subscription_id = table.Column<long>(type: "bigint", nullable: true),
                    registered_by = table.Column<long>(type: "bigint", nullable: true),
                    joined_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_waitlist", x => x.id);
                    table.ForeignKey(
                        name: "FK_class_waitlist_class_sessions_session_id",
                        column: x => x.session_id,
                        principalSchema: "dbo",
                        principalTable: "class_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_waitlist_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "dbo",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_waitlist_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_waitlist_member_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalSchema: "dbo",
                        principalTable: "member_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_waitlist_users_registered_by",
                        column: x => x.registered_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_refunds",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    payment_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false),
                    idempotency_key = table.Column<string>(type: "citext", maxLength: 100, nullable: false),
                    provider_refund_id = table.Column<string>(type: "citext", maxLength: 150, nullable: true),
                    external_reference = table.Column<string>(type: "citext", maxLength: 150, nullable: true),
                    reason = table.Column<string>(type: "citext", maxLength: 500, nullable: false),
                    bank_name = table.Column<string>(type: "citext", maxLength: 100, nullable: true),
                    bank_account_number = table.Column<string>(type: "citext", maxLength: 50, nullable: true),
                    bank_account_name = table.Column<string>(type: "citext", maxLength: 150, nullable: true),
                    requested_by = table.Column<long>(type: "bigint", nullable: false),
                    reviewed_by = table.Column<long>(type: "bigint", nullable: true),
                    review_note = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    transfer_proof_url = table.Column<string>(type: "text", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_refunds", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_refunds_payments_payment_id",
                        column: x => x.payment_id,
                        principalSchema: "dbo",
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_refunds_users_requested_by",
                        column: x => x.requested_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_refunds_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_items",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    package_id = table.Column<long>(type: "bigint", nullable: true),
                    subscription_id = table.Column<long>(type: "bigint", nullable: true),
                    enrollment_id = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "citext", maxLength: 500, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoice_items_class_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "dbo",
                        principalTable: "class_enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_items_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalSchema: "dbo",
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_items_member_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalSchema: "dbo",
                        principalTable: "member_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_items_membership_packages_package_id",
                        column: x => x.package_id,
                        principalSchema: "dbo",
                        principalTable: "membership_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "session_bookings",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<long>(type: "bigint", nullable: false),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    enrollment_id = table.Column<long>(type: "bigint", nullable: true),
                    subscription_id = table.Column<long>(type: "bigint", nullable: true),
                    registered_by = table.Column<long>(type: "bigint", nullable: true),
                    booked_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    status = table.Column<string>(type: "citext", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_bookings", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_bookings_class_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "dbo",
                        principalTable: "class_enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_session_bookings_class_sessions_session_id",
                        column: x => x.session_id,
                        principalSchema: "dbo",
                        principalTable: "class_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_session_bookings_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_session_bookings_member_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalSchema: "dbo",
                        principalTable: "member_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_session_bookings_users_registered_by",
                        column: x => x.registered_by,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "training_results",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    member_id = table.Column<long>(type: "bigint", nullable: false),
                    coach_id = table.Column<long>(type: "bigint", nullable: false),
                    class_id = table.Column<long>(type: "bigint", nullable: true),
                    body_fat_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    muscle_mass = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    workout_result = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    coach_feedback = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    session_id = table.Column<long>(type: "bigint", nullable: true),
                    training_plan_exercise_id = table.Column<long>(type: "bigint", nullable: true),
                    exercise_id = table.Column<long>(type: "bigint", nullable: true),
                    training_plan_id = table.Column<long>(type: "bigint", nullable: true),
                    result_date = table.Column<DateOnly>(type: "date", nullable: false),
                    sets_completed = table.Column<int>(type: "integer", nullable: true),
                    repetitions_completed = table.Column<int>(type: "integer", nullable: true),
                    weight = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    calories_burned = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    performance_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_training_results_class_sessions_session_id",
                        column: x => x.session_id,
                        principalSchema: "dbo",
                        principalTable: "class_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_training_results_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "dbo",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_training_results_coach_profiles_coach_id",
                        column: x => x.coach_id,
                        principalSchema: "dbo",
                        principalTable: "coach_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_training_results_exercises_exercise_id",
                        column: x => x.exercise_id,
                        principalSchema: "dbo",
                        principalTable: "exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_training_results_member_profiles_member_id",
                        column: x => x.member_id,
                        principalSchema: "dbo",
                        principalTable: "member_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_training_results_training_plan_exercises_training_plan_exer~",
                        column: x => x.training_plan_exercise_id,
                        principalSchema: "dbo",
                        principalTable: "training_plan_exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_training_results_training_plans_training_plan_id",
                        column: x => x.training_plan_id,
                        principalSchema: "dbo",
                        principalTable: "training_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_activation_tokens_token_hash",
                schema: "dbo",
                table: "account_activation_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_activation_tokens_user_id_created_at",
                schema: "dbo",
                table: "account_activation_tokens",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversations_member_id",
                schema: "dbo",
                table: "ai_conversations",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversations_user_id",
                schema: "dbo",
                table: "ai_conversations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_exercise_recommendations_conversation_id",
                schema: "dbo",
                table: "ai_exercise_recommendations",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_exercise_recommendations_exercise_id",
                schema: "dbo",
                table: "ai_exercise_recommendations",
                column: "exercise_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_exercise_recommendations_member_id",
                schema: "dbo",
                table: "ai_exercise_recommendations",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_exercise_recommendations_requested_by",
                schema: "dbo",
                table: "ai_exercise_recommendations",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "IX_ai_exercise_recommendations_training_plan_id",
                schema: "dbo",
                table: "ai_exercise_recommendations",
                column: "training_plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_messages_conversation_id",
                schema: "dbo",
                table: "ai_messages",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "IX_assignment_submissions_assignment_id",
                schema: "dbo",
                table: "assignment_submissions",
                column: "assignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_assignment_submissions_member_id",
                schema: "dbo",
                table: "assignment_submissions",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_class_id",
                schema: "dbo",
                table: "assignments",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_coach_id",
                schema: "dbo",
                table: "assignments",
                column: "coach_id");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_member_id",
                schema: "dbo",
                table: "assignments",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_checked_in_by",
                schema: "dbo",
                table: "attendance",
                column: "checked_in_by");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_member_id",
                schema: "dbo",
                table: "attendance",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_session_id",
                schema: "dbo",
                table: "attendance",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_center_id_created_at",
                schema: "dbo",
                table: "audit_logs",
                columns: new[] { "center_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_user_id",
                schema: "dbo",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_center_checkins_center_id",
                schema: "dbo",
                table: "center_checkins",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "IX_center_checkins_checked_in_by",
                schema: "dbo",
                table: "center_checkins",
                column: "checked_in_by");

            migrationBuilder.CreateIndex(
                name: "IX_center_checkins_member_id",
                schema: "dbo",
                table: "center_checkins",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_coaches_coach_id",
                schema: "dbo",
                table: "class_coaches",
                column: "coach_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_enrollments_class_id_member_id",
                schema: "dbo",
                table: "class_enrollments",
                columns: new[] { "class_id", "member_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_class_enrollments_member_id",
                schema: "dbo",
                table: "class_enrollments",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_enrollments_registered_by",
                schema: "dbo",
                table: "class_enrollments",
                column: "registered_by");

            migrationBuilder.CreateIndex(
                name: "IX_class_enrollments_subscription_id",
                schema: "dbo",
                table: "class_enrollments",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_schedules_class_id",
                schema: "dbo",
                table: "class_schedules",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_schedules_room_id",
                schema: "dbo",
                table: "class_schedules",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_sessions_class_id",
                schema: "dbo",
                table: "class_sessions",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_sessions_coach_id",
                schema: "dbo",
                table: "class_sessions",
                column: "coach_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_sessions_room_id",
                schema: "dbo",
                table: "class_sessions",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_sessions_schedule_id",
                schema: "dbo",
                table: "class_sessions",
                column: "schedule_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_waitlist_class_id",
                schema: "dbo",
                table: "class_waitlist",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_waitlist_member_id",
                schema: "dbo",
                table: "class_waitlist",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_waitlist_registered_by",
                schema: "dbo",
                table: "class_waitlist",
                column: "registered_by");

            migrationBuilder.CreateIndex(
                name: "IX_class_waitlist_session_id_member_id",
                schema: "dbo",
                table: "class_waitlist",
                columns: new[] { "session_id", "member_id" },
                unique: true,
                filter: "\"session_id\" IS NOT NULL AND \"status\" = 'Waiting'");

            migrationBuilder.CreateIndex(
                name: "IX_class_waitlist_subscription_id",
                schema: "dbo",
                table: "class_waitlist",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_classes_center_id",
                schema: "dbo",
                table: "classes",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "IX_classes_room_id",
                schema: "dbo",
                table: "classes",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "IX_classes_sport_id",
                schema: "dbo",
                table: "classes",
                column: "sport_id");

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
                name: "IX_coach_leaves_coach_id",
                schema: "dbo",
                table: "coach_leaves",
                column: "coach_id");

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
                name: "IX_coach_profiles_center_id",
                schema: "dbo",
                table: "coach_profiles",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "IX_coach_profiles_coach_code",
                schema: "dbo",
                table: "coach_profiles",
                column: "coach_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coach_profiles_user_id",
                schema: "dbo",
                table: "coach_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coach_reviews_coach_id_member_id_work_event_id",
                schema: "dbo",
                table: "coach_reviews",
                columns: new[] { "coach_id", "member_id", "work_event_id" },
                unique: true,
                filter: "\"work_event_id\" IS NOT NULL");

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
                name: "IX_coach_sports_sport_id",
                schema: "dbo",
                table: "coach_sports",
                column: "sport_id");

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
                name: "IX_communication_deliveries_status_next_attempt_at_lease_until",
                schema: "dbo",
                table: "communication_deliveries",
                columns: new[] { "status", "next_attempt_at", "lease_until" });

            migrationBuilder.CreateIndex(
                name: "IX_email_verification_tokens_email_created_at",
                schema: "dbo",
                table: "email_verification_tokens",
                columns: new[] { "email", "created_at" });

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
                name: "IX_exercises_sport_id",
                schema: "dbo",
                table: "exercises",
                column: "sport_id");

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
                name: "IX_invoice_items_enrollment_id",
                schema: "dbo",
                table: "invoice_items",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_invoice_id",
                schema: "dbo",
                table: "invoice_items",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_package_id",
                schema: "dbo",
                table: "invoice_items",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_subscription_id",
                schema: "dbo",
                table: "invoice_items",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_center_id",
                schema: "dbo",
                table: "invoices",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_created_by",
                schema: "dbo",
                table: "invoices",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_idempotency_key",
                schema: "dbo",
                table: "invoices",
                column: "idempotency_key",
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_invoice_number",
                schema: "dbo",
                table: "invoices",
                column: "invoice_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_member_id",
                schema: "dbo",
                table: "invoices",
                column: "member_id");

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
                name: "IX_member_profiles_center_id",
                schema: "dbo",
                table: "member_profiles",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "IX_member_profiles_member_code",
                schema: "dbo",
                table: "member_profiles",
                column: "member_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_member_profiles_user_id",
                schema: "dbo",
                table: "member_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_member_progress_reviews_coach_id",
                schema: "dbo",
                table: "member_progress_reviews",
                column: "coach_id");

            migrationBuilder.CreateIndex(
                name: "IX_member_progress_reviews_member_id",
                schema: "dbo",
                table: "member_progress_reviews",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_member_progress_reviews_training_plan_id",
                schema: "dbo",
                table: "member_progress_reviews",
                column: "training_plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_member_subscriptions_member_id",
                schema: "dbo",
                table: "member_subscriptions",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_member_subscriptions_package_id",
                schema: "dbo",
                table: "member_subscriptions",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "IX_membership_packages_center_id_name",
                schema: "dbo",
                table: "membership_packages",
                columns: new[] { "center_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_reminder_key",
                schema: "dbo",
                table: "notifications",
                column: "reminder_key",
                unique: true,
                filter: "\"reminder_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_sender_id",
                schema: "dbo",
                table: "notifications",
                column: "sender_id");

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
                name: "IX_password_reset_tokens_token",
                schema: "dbo",
                table: "password_reset_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_user_id",
                schema: "dbo",
                table: "password_reset_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_idempotency_key",
                schema: "dbo",
                table: "payment_refunds",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_payment_id",
                schema: "dbo",
                table: "payment_refunds",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_provider_refund_id",
                schema: "dbo",
                table: "payment_refunds",
                column: "provider_refund_id",
                unique: true,
                filter: "\"provider_refund_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_requested_by",
                schema: "dbo",
                table: "payment_refunds",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_reviewed_by",
                schema: "dbo",
                table: "payment_refunds",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_payments_gateway_reference",
                schema: "dbo",
                table: "payments",
                column: "gateway_reference",
                unique: true,
                filter: "\"gateway_reference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payments_idempotency_key",
                schema: "dbo",
                table: "payments",
                column: "idempotency_key",
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payments_invoice_id",
                schema: "dbo",
                table: "payments",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_member_id",
                schema: "dbo",
                table: "payments",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_processed_by",
                schema: "dbo",
                table: "payments",
                column: "processed_by");

            migrationBuilder.CreateIndex(
                name: "IX_payments_provider_transaction_id",
                schema: "dbo",
                table: "payments",
                column: "provider_transaction_id",
                unique: true,
                filter: "\"provider_transaction_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payments_refund_approved_by",
                schema: "dbo",
                table: "payments",
                column: "refund_approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_payments_transaction_code",
                schema: "dbo",
                table: "payments",
                column: "transaction_code",
                unique: true,
                filter: "\"transaction_code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_code",
                schema: "dbo",
                table: "permissions",
                column: "code",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_permission_id",
                schema: "dbo",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_name",
                schema: "dbo",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rooms_center_id",
                schema: "dbo",
                table: "rooms",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "IX_session_bookings_enrollment_id",
                schema: "dbo",
                table: "session_bookings",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "IX_session_bookings_member_id",
                schema: "dbo",
                table: "session_bookings",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_session_bookings_registered_by",
                schema: "dbo",
                table: "session_bookings",
                column: "registered_by");

            migrationBuilder.CreateIndex(
                name: "IX_session_bookings_session_id_member_id",
                schema: "dbo",
                table: "session_bookings",
                columns: new[] { "session_id", "member_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_bookings_subscription_id_status_session_id",
                schema: "dbo",
                table: "session_bookings",
                columns: new[] { "subscription_id", "status", "session_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sports_center_id",
                schema: "dbo",
                table: "sports",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "IX_staff_compensation_policies_center_id",
                schema: "dbo",
                table: "staff_compensation_policies",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "IX_staff_compensation_policies_updated_by",
                schema: "dbo",
                table: "staff_compensation_policies",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "IX_staff_compensation_policies_user_id_status",
                schema: "dbo",
                table: "staff_compensation_policies",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_profiles_center_id",
                schema: "dbo",
                table: "staff_profiles",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "IX_staff_profiles_staff_code",
                schema: "dbo",
                table: "staff_profiles",
                column: "staff_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_profiles_user_id",
                schema: "dbo",
                table: "staff_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_support_request_messages_request_id",
                schema: "dbo",
                table: "support_request_messages",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_support_request_messages_sender_id",
                schema: "dbo",
                table: "support_request_messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "IX_support_requests_center_id_status_created_at",
                schema: "dbo",
                table: "support_requests",
                columns: new[] { "center_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_support_requests_handled_by",
                schema: "dbo",
                table: "support_requests",
                column: "handled_by");

            migrationBuilder.CreateIndex(
                name: "IX_support_requests_member_id",
                schema: "dbo",
                table: "support_requests",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_support_requests_requester_id_created_at",
                schema: "dbo",
                table: "support_requests",
                columns: new[] { "requester_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_setting_key",
                schema: "dbo",
                table: "system_settings",
                column: "setting_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_updated_by",
                schema: "dbo",
                table: "system_settings",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "IX_training_plan_exercises_exercise_id",
                schema: "dbo",
                table: "training_plan_exercises",
                column: "exercise_id");

            migrationBuilder.CreateIndex(
                name: "IX_training_plan_exercises_training_plan_id",
                schema: "dbo",
                table: "training_plan_exercises",
                column: "training_plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_training_plans_class_id",
                schema: "dbo",
                table: "training_plans",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_training_plans_coach_id",
                schema: "dbo",
                table: "training_plans",
                column: "coach_id");

            migrationBuilder.CreateIndex(
                name: "IX_training_plans_member_id",
                schema: "dbo",
                table: "training_plans",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_training_results_class_id",
                schema: "dbo",
                table: "training_results",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_training_results_coach_id",
                schema: "dbo",
                table: "training_results",
                column: "coach_id");

            migrationBuilder.CreateIndex(
                name: "IX_training_results_exercise_id",
                schema: "dbo",
                table: "training_results",
                column: "exercise_id");

            migrationBuilder.CreateIndex(
                name: "IX_training_results_member_id",
                schema: "dbo",
                table: "training_results",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_training_results_session_id",
                schema: "dbo",
                table: "training_results",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_training_results_training_plan_exercise_id",
                schema: "dbo",
                table: "training_results",
                column: "training_plan_exercise_id");

            migrationBuilder.CreateIndex(
                name: "IX_training_results_training_plan_id",
                schema: "dbo",
                table: "training_results",
                column: "training_plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_uploaded_media_center_id_created_at",
                schema: "dbo",
                table: "uploaded_media",
                columns: new[] { "center_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_uploaded_media_drive_file_id",
                schema: "dbo",
                table: "uploaded_media",
                column: "drive_file_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_notification_id",
                schema: "dbo",
                table: "user_notifications",
                column: "notification_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_user_id",
                schema: "dbo",
                table: "user_notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                schema: "dbo",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_google_subject",
                schema: "dbo",
                table: "users",
                column: "google_subject",
                unique: true,
                filter: "\"google_subject\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_users_phone",
                schema: "dbo",
                table: "users",
                column: "phone",
                unique: true,
                filter: "\"phone\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_users_role_id",
                schema: "dbo",
                table: "users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                schema: "dbo",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_activation_tokens",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ai_exercise_recommendations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ai_messages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "assignment_submissions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "attendance",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "center_checkins",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "class_coaches",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "class_waitlist",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "coach_compensation_policies",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "coach_leaves",
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
                name: "coach_sports",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "communication_deliveries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "email_verification_tokens",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "equipment_issues",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "food_diary_entries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "invoice_items",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "member_health_records",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "member_progress_reviews",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "nutrition_plans",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "password_reset_tokens",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "payment_refunds",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "resource_bookings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "session_bookings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "staff_compensation_policies",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "staff_profiles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "support_request_messages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "system_settings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "training_results",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "uploaded_media",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "user_notifications",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ai_conversations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "assignments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "coach_work_events",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "facility_resources",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "class_enrollments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "support_requests",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "class_sessions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "training_plan_exercises",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "invoices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "member_subscriptions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "class_schedules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "exercises",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "training_plans",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "membership_packages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "classes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "coach_profiles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "member_profiles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "rooms",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "sports",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "users",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "centers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "dbo");
        }
    }
}
