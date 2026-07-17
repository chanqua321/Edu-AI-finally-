using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduAI.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderQuotaFieldsToPaymentPackage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DailyOllamaQuestions",
                table: "PaymentPackages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyGeminiQuestions",
                table: "PaymentPackages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE PaymentPackages
                SET MonthlyGeminiQuestions = CASE Id
                        WHEN 'Premium' THEN 40
                        WHEN 'Enterprise' THEN 150
                        ELSE 0
                    END,
                    DailyOllamaQuestions = CASE Id
                        WHEN 'Free' THEN 1
                        WHEN 'Premium' THEN 5
                        WHEN 'Enterprise' THEN 20
                        ELSE 0
                    END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyOllamaQuestions",
                table: "PaymentPackages");

            migrationBuilder.DropColumn(
                name: "MonthlyGeminiQuestions",
                table: "PaymentPackages");
        }
    }
}
