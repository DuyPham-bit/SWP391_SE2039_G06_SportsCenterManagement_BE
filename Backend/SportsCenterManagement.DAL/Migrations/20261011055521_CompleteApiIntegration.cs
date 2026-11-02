using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterManagement.DAL.Migrations
{
    /// <inheritdoc />
    public partial class CompleteApiIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "google_subject",
                schema: "dbo",
                table: "users",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "body_fat_percentage",
                schema: "dbo",
                table: "training_results",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "class_id",
                schema: "dbo",
                table: "training_results",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "coach_feedback",
                schema: "dbo",
                table: "training_results",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "muscle_mass",
                schema: "dbo",
                table: "training_results",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "workout_result",
                schema: "dbo",
                table: "training_results",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "member_id",
                schema: "dbo",
                table: "training_plans",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "exercises_json",
                schema: "dbo",
                table: "training_plans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fitness_condition",
                schema: "dbo",
                table: "training_plans",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_google_subject",
                schema: "dbo",
                table: "users",
                column: "google_subject",
                unique: true,
                filter: "[google_subject] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_training_results_class_id",
                schema: "dbo",
                table: "training_results",
                column: "class_id");

            migrationBuilder.AddForeignKey(
                name: "FK_training_results_classes_class_id",
                schema: "dbo",
                table: "training_results",
                column: "class_id",
                principalSchema: "dbo",
                principalTable: "classes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không ép giáo án chung về member_id=0 vì sẽ làm mất ý nghĩa dữ liệu và vi phạm FK.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.training_plans WHERE member_id IS NULL) THROW 51000, 'Export or migrate class training plans before rollback.', 1;");
            migrationBuilder.DropForeignKey(
                name: "FK_training_results_classes_class_id",
                schema: "dbo",
                table: "training_results");

            migrationBuilder.DropIndex(
                name: "IX_users_google_subject",
                schema: "dbo",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_training_results_class_id",
                schema: "dbo",
                table: "training_results");

            migrationBuilder.DropColumn(
                name: "google_subject",
                schema: "dbo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "body_fat_percentage",
                schema: "dbo",
                table: "training_results");

            migrationBuilder.DropColumn(
                name: "class_id",
                schema: "dbo",
                table: "training_results");

            migrationBuilder.DropColumn(
                name: "coach_feedback",
                schema: "dbo",
                table: "training_results");

            migrationBuilder.DropColumn(
                name: "muscle_mass",
                schema: "dbo",
                table: "training_results");

            migrationBuilder.DropColumn(
                name: "workout_result",
                schema: "dbo",
                table: "training_results");

            migrationBuilder.DropColumn(
                name: "exercises_json",
                schema: "dbo",
                table: "training_plans");

            migrationBuilder.DropColumn(
                name: "fitness_condition",
                schema: "dbo",
                table: "training_plans");

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
        }
    }
}
