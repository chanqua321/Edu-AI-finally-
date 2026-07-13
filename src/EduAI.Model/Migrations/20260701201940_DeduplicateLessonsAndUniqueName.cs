using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduAI.Model.Migrations
{
    /// <inheritdoc />
    public partial class DeduplicateLessonsAndUniqueName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE d SET d.LessonId = x.KeepId
                FROM Documents d
                INNER JOIN (
                    SELECT l.Id AS DupeId, k.KeepId
                    FROM Lessons l
                    INNER JOIN (
                        SELECT ChapterId, Name, MIN(Id) AS KeepId
                        FROM Lessons
                        GROUP BY ChapterId, Name
                    ) k ON l.ChapterId = k.ChapterId AND l.Name = k.Name
                    WHERE l.Id <> k.KeepId
                ) x ON d.LessonId = x.DupeId;

                DELETE FROM Lessons
                WHERE Id IN (
                    SELECT l.Id
                    FROM Lessons l
                    INNER JOIN (
                        SELECT ChapterId, Name, MIN(Id) AS KeepId
                        FROM Lessons
                        GROUP BY ChapterId, Name
                    ) k ON l.ChapterId = k.ChapterId AND l.Name = k.Name
                    WHERE l.Id <> k.KeepId
                );
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_ChapterId_Name",
                table: "Lessons",
                columns: new[] { "ChapterId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lessons_ChapterId_Name",
                table: "Lessons");
        }
    }
}
