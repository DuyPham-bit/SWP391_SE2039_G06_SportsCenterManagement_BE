using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCampusMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<bool>(
                name: "bookable",
                schema: "dbo",
                table: "rooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "floor_no",
                schema: "dbo",
                table: "rooms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "map_depth_m",
                schema: "dbo",
                table: "rooms",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "map_kind",
                schema: "dbo",
                table: "rooms",
                type: "citext",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "map_rotation_deg",
                schema: "dbo",
                table: "rooms",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "map_width_m",
                schema: "dbo",
                table: "rooms",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "map_x",
                schema: "dbo",
                table: "rooms",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "map_z",
                schema: "dbo",
                table: "rooms",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_en",
                schema: "dbo",
                table: "rooms",
                type: "citext",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "zone_id",
                schema: "dbo",
                table: "rooms",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "campus_zones",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    center_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "citext", maxLength: 20, nullable: false),
                    kind = table.Column<string>(type: "citext", maxLength: 20, nullable: false),
                    name_vi = table.Column<string>(type: "citext", maxLength: 120, nullable: false),
                    name_en = table.Column<string>(type: "citext", maxLength: 120, nullable: false),
                    description_vi = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    description_en = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    color_hex = table.Column<string>(type: "citext", maxLength: 9, nullable: false),
                    pos_x = table.Column<double>(type: "double precision", nullable: false),
                    pos_z = table.Column<double>(type: "double precision", nullable: false),
                    width_m = table.Column<double>(type: "double precision", nullable: false),
                    depth_m = table.Column<double>(type: "double precision", nullable: false),
                    building_width_m = table.Column<double>(type: "double precision", nullable: false),
                    building_depth_m = table.Column<double>(type: "double precision", nullable: false),
                    floors = table.Column<int>(type: "integer", nullable: false),
                    open_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    close_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    status = table.Column<string>(type: "citext", maxLength: 20, nullable: false),
                    image_url = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    amenities_json = table.Column<string>(type: "citext", nullable: false),
                    features_json = table.Column<string>(type: "citext", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campus_zones", x => x.id);
                    table.ForeignKey(
                        name: "FK_campus_zones_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "dbo",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rooms_zone_id",
                schema: "dbo",
                table: "rooms",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "IX_campus_zones_center_id_code",
                schema: "dbo",
                table: "campus_zones",
                columns: new[] { "center_id", "code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_rooms_campus_zones_zone_id",
                schema: "dbo",
                table: "rooms",
                column: "zone_id",
                principalSchema: "dbo",
                principalTable: "campus_zones",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rooms_campus_zones_zone_id",
                schema: "dbo",
                table: "rooms");

            migrationBuilder.DropTable(
                name: "campus_zones",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_rooms_zone_id",
                schema: "dbo",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "bookable",
                schema: "dbo",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "floor_no",
                schema: "dbo",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "map_depth_m",
                schema: "dbo",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "map_kind",
                schema: "dbo",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "map_rotation_deg",
                schema: "dbo",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "map_width_m",
                schema: "dbo",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "map_x",
                schema: "dbo",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "map_z",
                schema: "dbo",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "name_en",
                schema: "dbo",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "zone_id",
                schema: "dbo",
                table: "rooms");
        }
    }
}
