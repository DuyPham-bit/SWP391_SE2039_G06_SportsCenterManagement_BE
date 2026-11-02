using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadedMediaTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "uploaded_media",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    drive_file_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    file_name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    mime_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: true),
                    file_url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    web_view_link = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    uploaded_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uploaded_media", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "uploaded_media",
                schema: "dbo");
        }
    }
}
