using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduAI.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentLastModifiedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastModifiedByUserId",
                table: "Documents",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_LastModifiedByUserId",
                table: "Documents",
                column: "LastModifiedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_AspNetUsers_LastModifiedByUserId",
                table: "Documents",
                column: "LastModifiedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_AspNetUsers_LastModifiedByUserId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_LastModifiedByUserId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "LastModifiedByUserId",
                table: "Documents");
        }
    }
}
