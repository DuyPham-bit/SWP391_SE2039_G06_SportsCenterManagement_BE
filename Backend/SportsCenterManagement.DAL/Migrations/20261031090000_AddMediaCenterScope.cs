using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsCenterManagement.DAL.Context;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations;

[DbContext(typeof(SportsCenterDbContext))]
[Migration("20261031090000_AddMediaCenterScope")]
public sealed class AddMediaCenterScope : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(name: "center_id", schema: "dbo", table: "uploaded_media", type: "bigint", nullable: true);
        // Legacy uploads with a recorded owner can use that owner's authoritative current center.
        // Unknown owners remain unassigned, visible only to Admin.
        migrationBuilder.Sql("""
            UPDATE media SET center_id = COALESCE(
                (SELECT s.center_id FROM dbo.staff_profiles s WHERE s.user_id = media.uploaded_by_user_id AND s.status = 'Active'),
                (SELECT c.center_id FROM dbo.coach_profiles c WHERE c.user_id = media.uploaded_by_user_id AND c.status = 'Active'),
                (SELECT m.center_id FROM dbo.member_profiles m WHERE m.user_id = media.uploaded_by_user_id))
            FROM dbo.uploaded_media media WHERE media.uploaded_by_user_id IS NOT NULL;
            """);
        migrationBuilder.CreateIndex(name: "IX_uploaded_media_center_id_created_at", schema: "dbo", table: "uploaded_media", columns: new[] { "center_id", "created_at" });
        migrationBuilder.CreateIndex(name: "IX_uploaded_media_drive_file_id", schema: "dbo", table: "uploaded_media", column: "drive_file_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_uploaded_media_center_id_created_at", schema: "dbo", table: "uploaded_media");
        migrationBuilder.DropIndex(name: "IX_uploaded_media_drive_file_id", schema: "dbo", table: "uploaded_media");
        migrationBuilder.DropColumn(name: "center_id", schema: "dbo", table: "uploaded_media");
    }
}
