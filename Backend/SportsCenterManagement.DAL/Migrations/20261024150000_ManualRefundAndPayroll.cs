using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsCenterManagement.DAL.Context;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations;

[DbContext(typeof(SportsCenterDbContext))]
[Migration("20261024150000_ManualRefundAndPayroll")]
public sealed class ManualRefundAndPayroll : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "bank_name", schema: "dbo", table: "payment_refunds", type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "bank_account_number", schema: "dbo", table: "payment_refunds", type: "nvarchar(50)", maxLength: 50, nullable: true);
        migrationBuilder.AddColumn<string>(name: "bank_account_name", schema: "dbo", table: "payment_refunds", type: "nvarchar(150)", maxLength: 150, nullable: true);
        migrationBuilder.AddColumn<long>(name: "reviewed_by", schema: "dbo", table: "payment_refunds", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<string>(name: "review_note", schema: "dbo", table: "payment_refunds", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<string>(name: "transfer_proof_url", schema: "dbo", table: "payment_refunds", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.DropColumn(name: "priority", schema: "dbo", table: "support_requests");
        migrationBuilder.CreateIndex(name: "IX_payment_refunds_reviewed_by", schema: "dbo", table: "payment_refunds", column: "reviewed_by");
        migrationBuilder.AddForeignKey(name: "FK_payment_refunds_users_reviewed_by", schema: "dbo", table: "payment_refunds", column: "reviewed_by", principalSchema: "dbo", principalTable: "users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

        migrationBuilder.CreateTable(
            name: "staff_compensation_policies",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                user_id = table.Column<long>(type: "bigint", nullable: false),
                center_id = table.Column<long>(type: "bigint", nullable: true),
                monthly_base_salary = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                monthly_allowance = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                updated_by = table.Column<long>(type: "bigint", nullable: false),
                updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_staff_compensation_policies", x => x.Id);
                table.ForeignKey(name: "FK_staff_compensation_policies_centers_center_id", column: x => x.center_id, principalSchema: "dbo", principalTable: "centers", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_staff_compensation_policies_users_updated_by", column: x => x.updated_by, principalSchema: "dbo", principalTable: "users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_staff_compensation_policies_users_user_id", column: x => x.user_id, principalSchema: "dbo", principalTable: "users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_staff_compensation_policies_center_id", schema: "dbo", table: "staff_compensation_policies", column: "center_id");
        migrationBuilder.CreateIndex(name: "IX_staff_compensation_policies_updated_by", schema: "dbo", table: "staff_compensation_policies", column: "updated_by");
        migrationBuilder.CreateIndex(name: "IX_staff_compensation_policies_user_id_status", schema: "dbo", table: "staff_compensation_policies", columns: new[] { "user_id", "status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "staff_compensation_policies", schema: "dbo");
        migrationBuilder.DropForeignKey(name: "FK_payment_refunds_users_reviewed_by", schema: "dbo", table: "payment_refunds");
        migrationBuilder.DropIndex(name: "IX_payment_refunds_reviewed_by", schema: "dbo", table: "payment_refunds");
        migrationBuilder.DropColumn(name: "bank_name", schema: "dbo", table: "payment_refunds");
        migrationBuilder.DropColumn(name: "bank_account_number", schema: "dbo", table: "payment_refunds");
        migrationBuilder.DropColumn(name: "bank_account_name", schema: "dbo", table: "payment_refunds");
        migrationBuilder.DropColumn(name: "reviewed_by", schema: "dbo", table: "payment_refunds");
        migrationBuilder.DropColumn(name: "review_note", schema: "dbo", table: "payment_refunds");
        migrationBuilder.DropColumn(name: "transfer_proof_url", schema: "dbo", table: "payment_refunds");
        migrationBuilder.AddColumn<string>(name: "priority", schema: "dbo", table: "support_requests", type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Normal");
    }
}
