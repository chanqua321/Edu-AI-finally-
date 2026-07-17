using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduAI.Model.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateSystemSettingsAndAiProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "PaymentPackages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DurationDays",
                table: "PaymentPackages",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "PaymentPackages",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecommended",
                table: "PaymentPackages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCostUsd",
                table: "AiUsageLogs",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "AiUsageLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DefaultChunkMode = table.Column<int>(type: "int", nullable: false),
                    DefaultChunkSize = table.Column<int>(type: "int", nullable: false),
                    DefaultChunkOverlap = table.Column<int>(type: "int", nullable: false),
                    RetrievalTopK = table.Column<int>(type: "int", nullable: false),
                    MaxChatHistory = table.Column<int>(type: "int", nullable: false),
                    EnableCitation = table.Column<bool>(type: "bit", nullable: false),
                    GenerationProvider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EnableBenchmarkLogging = table.Column<bool>(type: "bit", nullable: false),
                    DefaultEmbeddingModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DefaultGenerationModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Temperature = table.Column<double>(type: "float(4)", precision: 4, scale: 2, nullable: false),
                    MaxOutputTokens = table.Column<int>(type: "int", nullable: false),
                    MaxUploadFileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    AllowedFileExtensions = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DefaultTimezone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DailyQuotaResetHour = table.Column<int>(type: "int", nullable: false),
                    CountFailedRequestsAgainstQuota = table.Column<bool>(type: "bit", nullable: false),
                    InputTokenPricePerMillion = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    OutputTokenPricePerMillion = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    EmbeddingPricePerMillion = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    EnableLatencyLogging = table.Column<bool>(type: "bit", nullable: false),
                    EnableTokenLogging = table.Column<bool>(type: "bit", nullable: false),
                    EnableCostLogging = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSettings_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_UpdatedByUserId",
                table: "SystemSettings",
                column: "UpdatedByUserId");

            // Preserve chunk settings from legacy IndexingSettings before drop.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [IndexingSettings])
                BEGIN
                    INSERT INTO [SystemSettings] (
                        DefaultChunkMode, DefaultChunkSize, DefaultChunkOverlap,
                        RetrievalTopK, MaxChatHistory, EnableCitation, GenerationProvider, EnableBenchmarkLogging,
                        DefaultEmbeddingModel, DefaultGenerationModel, Temperature, MaxOutputTokens,
                        MaxUploadFileSizeBytes, AllowedFileExtensions, DefaultTimezone, DailyQuotaResetHour,
                        CountFailedRequestsAgainstQuota, InputTokenPricePerMillion, OutputTokenPricePerMillion,
                        EmbeddingPricePerMillion, EnableLatencyLogging, EnableTokenLogging, EnableCostLogging,
                        UpdatedAt, UpdatedByUserId)
                    SELECT TOP 1
                        ChunkMode, ChunkSize, ChunkOverlap,
                        5, 10, 1, N'Gemini', 1,
                        N'', N'', 0.7, 8192,
                        52428800, N'.pdf,.docx,.pptx,.txt', N'UTC', 0,
                        0, 0.075, 0.30, 0.01, 1, 1, 1,
                        UpdatedAt, UpdatedByUserId
                    FROM [IndexingSettings];
                END
                """);

            migrationBuilder.DropTable(name: "IndexingSettings");
            migrationBuilder.DropTable(name: "SubjectIndexingSettings");

            migrationBuilder.Sql("""
                UPDATE [PaymentPackages] SET DurationDays = 99999, DisplayOrder = 1, IsActive = 1 WHERE Id = 'Free';
                UPDATE [PaymentPackages] SET DurationDays = 30, DisplayOrder = 2, IsRecommended = 1, IsActive = 1 WHERE Id = 'Premium';
                UPDATE [PaymentPackages] SET DurationDays = 30, DisplayOrder = 3, IsActive = 1 WHERE Id = 'Enterprise';
                UPDATE [PaymentPackages] SET IsActive = 1 WHERE IsActive = 0;
                UPDATE [AiUsageLogs] SET Provider = N'Gemini' WHERE Provider IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "PaymentPackages");

            migrationBuilder.DropColumn(
                name: "DurationDays",
                table: "PaymentPackages");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "PaymentPackages");

            migrationBuilder.DropColumn(
                name: "IsRecommended",
                table: "PaymentPackages");

            migrationBuilder.DropColumn(
                name: "EstimatedCostUsd",
                table: "AiUsageLogs");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "AiUsageLogs");

            migrationBuilder.CreateTable(
                name: "IndexingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ChunkMode = table.Column<int>(type: "int", nullable: false),
                    ChunkOverlap = table.Column<int>(type: "int", nullable: false),
                    ChunkSize = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexingSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndexingSettings_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SubjectIndexingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ChunkMode = table.Column<int>(type: "int", nullable: false),
                    ChunkOverlap = table.Column<int>(type: "int", nullable: false),
                    ChunkSize = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectIndexingSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubjectIndexingSettings_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubjectIndexingSettings_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndexingSettings_UpdatedByUserId",
                table: "IndexingSettings",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectIndexingSettings_SubjectId",
                table: "SubjectIndexingSettings",
                column: "SubjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubjectIndexingSettings_UpdatedByUserId",
                table: "SubjectIndexingSettings",
                column: "UpdatedByUserId");
        }
    }
}
