using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduAI.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Chapters_ChapterId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ChapterId_FileName",
                table: "Documents");

            migrationBuilder.AddColumn<int>(
                name: "LessonId",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Lessons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChapterId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OrderNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lessons_Chapters_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "Chapters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_ChapterId",
                table: "Lessons",
                column: "ChapterId");

            // Backfill: every existing chapter gets a default lesson ("Bài 1"),
            // and all of that chapter's documents are moved into it.
            migrationBuilder.Sql(@"
                INSERT INTO Lessons (ChapterId, Name, OrderNumber, CreatedAt)
                SELECT c.Id, N'Bài 1', 1, SYSUTCDATETIME()
                FROM Chapters c;");

            migrationBuilder.Sql(@"
                UPDATE d
                SET d.LessonId = l.Id
                FROM Documents d
                INNER JOIN Lessons l ON l.ChapterId = d.ChapterId;");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ChapterId",
                table: "Documents",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_LessonId_FileName",
                table: "Documents",
                columns: new[] { "LessonId", "FileName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Chapters_ChapterId",
                table: "Documents",
                column: "ChapterId",
                principalTable: "Chapters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Lessons_LessonId",
                table: "Documents",
                column: "LessonId",
                principalTable: "Lessons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Chapters_ChapterId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Lessons_LessonId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "Lessons");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ChapterId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_LessonId_FileName",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "LessonId",
                table: "Documents");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ChapterId_FileName",
                table: "Documents",
                columns: new[] { "ChapterId", "FileName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Chapters_ChapterId",
                table: "Documents",
                column: "ChapterId",
                principalTable: "Chapters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
