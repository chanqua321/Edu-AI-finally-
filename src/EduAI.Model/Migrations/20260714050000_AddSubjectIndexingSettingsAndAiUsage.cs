using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduAI.Model.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260714050000_AddSubjectIndexingSettingsAndAiUsage")]
    public partial class AddSubjectIndexingSettingsAndAiUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CitedChunkIds",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiUsageLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    ChatSessionId = table.Column<int>(type: "int", nullable: true),
                    ChatMessageId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PromptTokens = table.Column<int>(type: "int", nullable: false),
                    CompletionTokens = table.Column<int>(type: "int", nullable: false),
                    TotalTokens = table.Column<int>(type: "int", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiUsageLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AiUsageLogs_ChatMessages_ChatMessageId",
                        column: x => x.ChatMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AiUsageLogs_ChatSessions_ChatSessionId",
                        column: x => x.ChatSessionId,
                        principalTable: "ChatSessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AiUsageLogs_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubjectIndexingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    ChunkMode = table.Column<int>(type: "int", nullable: false),
                    ChunkSize = table.Column<int>(type: "int", nullable: false),
                    ChunkOverlap = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true)
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
                name: "IX_AiUsageLogs_ChatMessageId",
                table: "AiUsageLogs",
                column: "ChatMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLogs_ChatSessionId",
                table: "AiUsageLogs",
                column: "ChatSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLogs_SubjectId_CreatedAt",
                table: "AiUsageLogs",
                columns: new[] { "SubjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLogs_UserId",
                table: "AiUsageLogs",
                column: "UserId");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsageLogs");

            migrationBuilder.DropTable(
                name: "SubjectIndexingSettings");

            migrationBuilder.DropColumn(
                name: "CitedChunkIds",
                table: "ChatMessages");
        }
    }
}
