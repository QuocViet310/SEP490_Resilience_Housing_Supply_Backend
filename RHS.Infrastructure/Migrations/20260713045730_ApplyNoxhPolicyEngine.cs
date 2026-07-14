using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApplyNoxhPolicyEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "PolicyConfigs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "General");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "PolicyConfigs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "PolicyConfigs",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublicAnnounceAt",
                table: "HousingProjects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageHousingAreaPerPerson",
                table: "HousingApplications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LatestAssessmentId",
                table: "HousingApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LotteryResult",
                table: "HousingApplications",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyIncome",
                table: "HousingApplications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SpouseMonthlyIncome",
                table: "HousingApplications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "EligibilityAssessments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonsJson",
                table: "EligibilityAssessments",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LotteryDraws",
                columns: table => new
                {
                    DrawId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DrawnBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DrawnAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalUnits = table.Column<int>(type: "int", nullable: false),
                    PriorityAllocated = table.Column<int>(type: "int", nullable: false),
                    RandomAllocated = table.Column<int>(type: "int", nullable: false),
                    TotalParticipants = table.Column<int>(type: "int", nullable: false),
                    RandomSeed = table.Column<int>(type: "int", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotteryDraws", x => x.DrawId);
                    table.ForeignKey(
                        name: "FK_LotteryDraws_HousingProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "HousingProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotteryDraws_Users_DrawnBy",
                        column: x => x.DrawnBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PolicyConfigs_Category",
                table: "PolicyConfigs",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityAssessments_ApplicationId",
                table: "EligibilityAssessments",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_LotteryDraws_DrawnAt",
                table: "LotteryDraws",
                column: "DrawnAt");

            migrationBuilder.CreateIndex(
                name: "IX_LotteryDraws_DrawnBy",
                table: "LotteryDraws",
                column: "DrawnBy");

            migrationBuilder.CreateIndex(
                name: "IX_LotteryDraws_ProjectId",
                table: "LotteryDraws",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_EligibilityAssessments_HousingApplications_ApplicationId",
                table: "EligibilityAssessments",
                column: "ApplicationId",
                principalTable: "HousingApplications",
                principalColumn: "ApplicationId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EligibilityAssessments_HousingApplications_ApplicationId",
                table: "EligibilityAssessments");

            migrationBuilder.DropTable(
                name: "LotteryDraws");

            migrationBuilder.DropIndex(
                name: "IX_PolicyConfigs_Category",
                table: "PolicyConfigs");

            migrationBuilder.DropIndex(
                name: "IX_EligibilityAssessments_ApplicationId",
                table: "EligibilityAssessments");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "PolicyConfigs");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "PolicyConfigs");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "PolicyConfigs");

            migrationBuilder.DropColumn(
                name: "PublicAnnounceAt",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "AverageHousingAreaPerPerson",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "LatestAssessmentId",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "LotteryResult",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "MonthlyIncome",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "SpouseMonthlyIncome",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "EligibilityAssessments");

            migrationBuilder.DropColumn(
                name: "ReasonsJson",
                table: "EligibilityAssessments");
        }
    }
}
