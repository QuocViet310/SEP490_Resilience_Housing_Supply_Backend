using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueReportAndProjectImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectImages_HousingProjects_HousingProjectId",
                table: "ProjectImages");

            migrationBuilder.DropIndex(
                name: "IX_ProjectImages_HousingProjectId",
                table: "ProjectImages");

            migrationBuilder.DropColumn(
                name: "HousingProjectId",
                table: "ProjectImages");

            migrationBuilder.RenameColumn(
                name: "ImageId",
                table: "ProjectImages",
                newName: "Id");

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "ProjectImages",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ProjectImages",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "ProjectImages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "IssueReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssueType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Open"),
                    ScreenshotUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueReports_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IssueReports_CreatedAt",
                table: "IssueReports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_IssueReports_IssueType",
                table: "IssueReports",
                column: "IssueType");

            migrationBuilder.CreateIndex(
                name: "IX_IssueReports_Status",
                table: "IssueReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_IssueReports_UserId",
                table: "IssueReports",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IssueReports");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ProjectImages");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "ProjectImages");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ProjectImages",
                newName: "ImageId");

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "ProjectImages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<Guid>(
                name: "HousingProjectId",
                table: "ProjectImages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectImages_HousingProjectId",
                table: "ProjectImages",
                column: "HousingProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectImages_HousingProjects_HousingProjectId",
                table: "ProjectImages",
                column: "HousingProjectId",
                principalTable: "HousingProjects",
                principalColumn: "Id");
        }
    }
}
