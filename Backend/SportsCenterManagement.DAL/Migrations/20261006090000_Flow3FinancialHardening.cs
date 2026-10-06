using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsCenterManagement.DAL.Context;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations;

[DbContext(typeof(SportsCenterDbContext))]
[Migration("20261006090000_Flow3FinancialHardening")]
public sealed class Flow3FinancialHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "idempotency_key", schema: "dbo", table: "invoices",
            type: "nvarchar(100)", maxLength: 100, nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "created_at", schema: "dbo", table: "payments",
            type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "gateway_reference", schema: "dbo", table: "payments",
            type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "provider_transaction_id", schema: "dbo", table: "payments",
            type: "nvarchar(150)", maxLength: 150, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "idempotency_key", schema: "dbo", table: "payments",
            type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "gateway_payment_url", schema: "dbo", table: "payments",
            type: "nvarchar(2048)", maxLength: 2048, nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "amount_received", schema: "dbo", table: "payments",
            type: "decimal(12,2)", precision: 12, scale: 2, nullable: true);

        migrationBuilder.Sql("UPDATE [dbo].[payments] SET [created_at] = COALESCE([paid_at], SYSUTCDATETIME()) WHERE [created_at] IS NULL;");
        migrationBuilder.Sql("UPDATE [dbo].[payments] SET [payment_status] = N'Succeeded' WHERE [payment_status] = N'Completed';");
        migrationBuilder.Sql("UPDATE [dbo].[invoices] SET [status] = N'Voided' WHERE [status] = N'Cancelled';");
        migrationBuilder.AlterColumn<DateTime>(
            name: "created_at", schema: "dbo", table: "payments",
            type: "datetime2", nullable: false,
            oldClrType: typeof(DateTime), oldType: "datetime2", oldNullable: true);
        migrationBuilder.AlterColumn<DateTime>(
            name: "paid_at", schema: "dbo", table: "payments",
            type: "datetime2", nullable: true,
            oldClrType: typeof(DateTime), oldType: "datetime2");

        migrationBuilder.CreateTable(
            name: "payment_refunds",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint").Annotation("SqlServer:Identity", "1, 1"),
                PaymentId = table.Column<long>(name: "payment_id", type: "bigint", nullable: false),
                Amount = table.Column<decimal>(name: "amount", type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                Status = table.Column<string>(name: "status", type: "nvarchar(30)", maxLength: 30, nullable: false),
                IdempotencyKey = table.Column<string>(name: "idempotency_key", type: "nvarchar(100)", maxLength: 100, nullable: false),
                ProviderRefundId = table.Column<string>(name: "provider_refund_id", type: "nvarchar(150)", maxLength: 150, nullable: true),
                ExternalReference = table.Column<string>(name: "external_reference", type: "nvarchar(150)", maxLength: 150, nullable: true),
                Reason = table.Column<string>(name: "reason", type: "nvarchar(500)", maxLength: 500, nullable: false),
                RequestedBy = table.Column<long>(name: "requested_by", type: "bigint", nullable: false),
                CreatedAt = table.Column<DateTime>(name: "created_at", type: "datetime2", nullable: false),
                ProcessedAt = table.Column<DateTime>(name: "processed_at", type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payment_refunds", x => x.Id);
                table.ForeignKey(
                    name: "FK_payment_refunds_payments_payment_id",
                    column: x => x.PaymentId,
                    principalTable: "payments",
                    principalColumn: "Id",
                    principalSchema: "dbo",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_payment_refunds_users_requested_by",
                    column: x => x.RequestedBy,
                    principalTable: "users",
                    principalColumn: "Id",
                    principalSchema: "dbo",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_invoices_idempotency_key", schema: "dbo", table: "invoices",
            column: "idempotency_key", unique: true, filter: "[idempotency_key] IS NOT NULL");
        migrationBuilder.CreateIndex(
            name: "IX_payments_gateway_reference", schema: "dbo", table: "payments",
            column: "gateway_reference", unique: true, filter: "[gateway_reference] IS NOT NULL");
        migrationBuilder.CreateIndex(
            name: "IX_payments_provider_transaction_id", schema: "dbo", table: "payments",
            column: "provider_transaction_id", unique: true, filter: "[provider_transaction_id] IS NOT NULL");
        migrationBuilder.CreateIndex(
            name: "IX_payments_idempotency_key", schema: "dbo", table: "payments",
            column: "idempotency_key", unique: true, filter: "[idempotency_key] IS NOT NULL");
        migrationBuilder.CreateIndex(
            name: "IX_payment_refunds_payment_id", schema: "dbo", table: "payment_refunds", column: "payment_id");
        migrationBuilder.CreateIndex(
            name: "IX_payment_refunds_requested_by", schema: "dbo", table: "payment_refunds", column: "requested_by");
        migrationBuilder.CreateIndex(
            name: "IX_payment_refunds_idempotency_key", schema: "dbo", table: "payment_refunds",
            column: "idempotency_key", unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_payment_refunds_provider_refund_id", schema: "dbo", table: "payment_refunds",
            column: "provider_refund_id", unique: true, filter: "[provider_refund_id] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE [dbo].[payments] SET [paid_at] = COALESCE([paid_at], [created_at], SYSUTCDATETIME()) WHERE [paid_at] IS NULL;");
        migrationBuilder.DropTable(name: "payment_refunds", schema: "dbo");
        migrationBuilder.DropIndex(name: "IX_invoices_idempotency_key", schema: "dbo", table: "invoices");
        migrationBuilder.DropIndex(name: "IX_payments_gateway_reference", schema: "dbo", table: "payments");
        migrationBuilder.DropIndex(name: "IX_payments_provider_transaction_id", schema: "dbo", table: "payments");
        migrationBuilder.DropIndex(name: "IX_payments_idempotency_key", schema: "dbo", table: "payments");

        migrationBuilder.DropColumn(name: "idempotency_key", schema: "dbo", table: "invoices");
        migrationBuilder.DropColumn(name: "created_at", schema: "dbo", table: "payments");
        migrationBuilder.DropColumn(name: "gateway_reference", schema: "dbo", table: "payments");
        migrationBuilder.DropColumn(name: "provider_transaction_id", schema: "dbo", table: "payments");
        migrationBuilder.DropColumn(name: "idempotency_key", schema: "dbo", table: "payments");
        migrationBuilder.DropColumn(name: "gateway_payment_url", schema: "dbo", table: "payments");
        migrationBuilder.DropColumn(name: "amount_received", schema: "dbo", table: "payments");
        migrationBuilder.AlterColumn<DateTime>(
            name: "paid_at", schema: "dbo", table: "payments",
            type: "datetime2", nullable: false,
            oldClrType: typeof(DateTime), oldType: "datetime2", oldNullable: true);
    }
}
