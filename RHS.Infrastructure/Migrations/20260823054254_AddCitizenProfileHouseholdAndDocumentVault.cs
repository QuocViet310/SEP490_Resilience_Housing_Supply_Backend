using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCitizenProfileHouseholdAndDocumentVault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AverageHousingAreaPerPerson",
                table: "Users",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentResidence",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EkycVerifiedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HousingStatus",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IdIssueDate",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdIssuePlace",
                table: "Users",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEkycVerified",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MaritalStatus",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyIncome",
                table: "Users",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nationality",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Occupation",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PermanentAddress",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaceOfOrigin",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriorityGroup",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpouseCitizenId",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SpouseDateOfBirth",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpouseFullName",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SpouseMonthlyIncome",
                table: "Users",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkPlace",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DependentReason",
                table: "HouseholdMembers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasMeritService",
                table: "HouseholdMembers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDependent",
                table: "HouseholdMembers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MeritDetails",
                table: "HouseholdMembers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyIncome",
                table: "HouseholdMembers",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Occupation",
                table: "HouseholdMembers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserDocuments",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VerificationStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "PENDING"),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDocuments", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_UserDocuments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserHouseholdMembers",
                columns: table => new
                {
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CitizenId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Relationship = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Occupation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MonthlyIncome = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsDependent = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DependentReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    HasMeritService = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MeritDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserHouseholdMembers", x => x.MemberId);
                    table.ForeignKey(
                        name: "FK_UserHouseholdMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserDocuments_UserId",
                table: "UserDocuments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDocuments_UserId_DocumentType",
                table: "UserDocuments",
                columns: new[] { "UserId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_UserHouseholdMembers_CitizenId",
                table: "UserHouseholdMembers",
                column: "CitizenId");

            migrationBuilder.CreateIndex(
                name: "IX_UserHouseholdMembers_UserId",
                table: "UserHouseholdMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserHouseholdMembers_UserId_CitizenId",
                table: "UserHouseholdMembers",
                columns: new[] { "UserId", "CitizenId" },
                unique: true,
                filter: "[CitizenId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserDocuments");

            migrationBuilder.DropTable(
                name: "UserHouseholdMembers");

            migrationBuilder.DropColumn(
                name: "AverageHousingAreaPerPerson",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CurrentResidence",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EkycVerifiedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HousingStatus",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IdIssueDate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IdIssuePlace",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsEkycVerified",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MaritalStatus",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MonthlyIncome",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Nationality",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Occupation",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PermanentAddress",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PlaceOfOrigin",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PriorityGroup",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SpouseCitizenId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SpouseDateOfBirth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SpouseFullName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SpouseMonthlyIncome",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WorkPlace",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DependentReason",
                table: "HouseholdMembers");

            migrationBuilder.DropColumn(
                name: "HasMeritService",
                table: "HouseholdMembers");

            migrationBuilder.DropColumn(
                name: "IsDependent",
                table: "HouseholdMembers");

            migrationBuilder.DropColumn(
                name: "MeritDetails",
                table: "HouseholdMembers");

            migrationBuilder.DropColumn(
                name: "MonthlyIncome",
                table: "HouseholdMembers");

            migrationBuilder.DropColumn(
                name: "Occupation",
                table: "HouseholdMembers");
        }
    }
}
