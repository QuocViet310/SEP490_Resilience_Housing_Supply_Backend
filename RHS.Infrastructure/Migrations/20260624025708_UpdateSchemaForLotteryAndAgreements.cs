using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSchemaForLotteryAndAgreements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "HousingProjects");

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
                name: "DepositAmount",
                table: "HousingProjects",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "LotteryDate",
                table: "HousingProjects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LotteryLocation",
                table: "HousingProjects",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Street",
                table: "HousingProjects",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Ward",
                table: "HousingProjects",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SlotCode",
                table: "HousingApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PrincipleAgreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PdfUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrincipleAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrincipleAgreements_HousingApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "HousingApplications",
                        principalColumn: "ApplicationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrincipleAgreements_ApplicationId",
                table: "PrincipleAgreements",
                column: "ApplicationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrincipleAgreements");

            migrationBuilder.DropColumn(
                name: "ManagedWard",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ResidentWard",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DepositAmount",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "LotteryDate",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "LotteryLocation",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "Street",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "Ward",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "SlotCode",
                table: "HousingApplications");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "HousingProjects",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }
    }
}
