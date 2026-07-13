using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduAI.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentIndexStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IndexError",
                table: "Documents",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IndexStatus",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "IndexedAt",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            // Backfill: documents that already have chunks are considered successfully indexed.
            migrationBuilder.Sql(@"
                UPDATE d
                SET d.IndexStatus = 2,
                    d.IndexedAt = ISNULL(d.UpdatedAt, d.CreatedAt)
                FROM Documents d
                WHERE EXISTS (SELECT 1 FROM DocumentChunks c WHERE c.DocumentId = d.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IndexError",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IndexStatus",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IndexedAt",
                table: "Documents");
        }
    }
}
