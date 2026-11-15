using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class DisableLegacyAdminLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Admin locks used to set only locked_until (+20 years) and keep status Active, which
            // "forgot password" could lift. Admin locks now also set status Disabled; align old rows.
            migrationBuilder.Sql(
                "UPDATE dbo.users SET status = 'Disabled', updated_at = now() " +
                "WHERE status = 'Active' AND locked_until > now() + interval '1 year';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
