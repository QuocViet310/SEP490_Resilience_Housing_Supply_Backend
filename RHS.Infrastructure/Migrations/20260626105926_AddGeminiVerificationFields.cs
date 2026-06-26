using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGeminiVerificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiModelUsed",
                table: "AIVerificationResults",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorDetails",
                table: "AIVerificationResults",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedAddress",
                table: "AIVerificationResults",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedCitizenId",
                table: "AIVerificationResults",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedDateOfBirth",
                table: "AIVerificationResults",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedFullName",
                table: "AIVerificationResults",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiModelUsed",
                table: "AIVerificationResults");

            migrationBuilder.DropColumn(
                name: "ErrorDetails",
                table: "AIVerificationResults");

            migrationBuilder.DropColumn(
                name: "ExtractedAddress",
                table: "AIVerificationResults");

            migrationBuilder.DropColumn(
                name: "ExtractedCitizenId",
                table: "AIVerificationResults");

            migrationBuilder.DropColumn(
                name: "ExtractedDateOfBirth",
                table: "AIVerificationResults");

            migrationBuilder.DropColumn(
                name: "ExtractedFullName",
                table: "AIVerificationResults");
        }
    }
}
