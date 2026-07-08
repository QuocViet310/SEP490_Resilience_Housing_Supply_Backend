using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDomainModelForWeek9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManagedWard",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ResidentWard",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EstimatedMonthlyIncome",
                table: "HousingApplications");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApplicationCloseDate",
                table: "HousingProjects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApplicationOpenDate",
                table: "HousingProjects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalDate",
                table: "HousingProjects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionNumber",
                table: "HousingProjects",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeveloperId",
                table: "HousingProjects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConfirmed",
                table: "HousingProjects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "HousingProjects",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HouseholdMembersCount",
                table: "HousingApplications",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "MaritalStatus",
                table: "HousingApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriorityGroup",
                table: "HousingApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptUrl",
                table: "HousingApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HousingProjects_DeveloperId",
                table: "HousingProjects",
                column: "DeveloperId");

            migrationBuilder.AddForeignKey(
                name: "FK_HousingProjects_Users_DeveloperId",
                table: "HousingProjects",
                column: "DeveloperId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Update RoleNames for existing Seed Data
            migrationBuilder.Sql("UPDATE Roles SET RoleName = 'Housing Developer' WHERE Id = '66666666-6666-6666-6666-666666666666';");
            migrationBuilder.Sql("UPDATE Roles SET RoleName = 'Department Of Construction' WHERE Id = '55555555-5555-5555-5555-555555555555';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HousingProjects_Users_DeveloperId",
                table: "HousingProjects");

            migrationBuilder.DropIndex(
                name: "IX_HousingProjects_DeveloperId",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "ApplicationCloseDate",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "ApplicationOpenDate",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "ApprovalDate",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "DecisionNumber",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "DeveloperId",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "IsConfirmed",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "HouseholdMembersCount",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "MaritalStatus",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "PriorityGroup",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "ReceiptUrl",
                table: "HousingApplications");

            migrationBuilder.AddColumn<string>(
                name: "ManagedWard",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidentWard",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedMonthlyIncome",
                table: "HousingApplications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Revert RoleNames for Seed Data
            migrationBuilder.Sql("UPDATE Roles SET RoleName = 'Verification Officer' WHERE Id = '66666666-6666-6666-6666-666666666666';");
            migrationBuilder.Sql("UPDATE Roles SET RoleName = 'Ward Manager' WHERE Id = '55555555-5555-5555-5555-555555555555';");
        }
    }
}
