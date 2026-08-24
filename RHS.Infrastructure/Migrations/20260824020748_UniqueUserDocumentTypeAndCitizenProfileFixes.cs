using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniqueUserDocumentTypeAndCitizenProfileFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserDocuments_UserId_DocumentType",
                table: "UserDocuments");

            migrationBuilder.CreateIndex(
                name: "IX_UserDocuments_UserId_DocumentType",
                table: "UserDocuments",
                columns: new[] { "UserId", "DocumentType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserDocuments_UserId_DocumentType",
                table: "UserDocuments");

            migrationBuilder.CreateIndex(
                name: "IX_UserDocuments_UserId_DocumentType",
                table: "UserDocuments",
                columns: new[] { "UserId", "DocumentType" });
        }
    }
}
