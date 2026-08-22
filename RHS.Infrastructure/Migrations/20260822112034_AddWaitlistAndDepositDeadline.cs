using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlistAndDepositDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DepositDeadline",
                table: "HousingApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WaitlistNumber",
                table: "HousingApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WaitlistPromotedAt",
                table: "HousingApplications",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepositDeadline",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "WaitlistNumber",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "WaitlistPromotedAt",
                table: "HousingApplications");
        }
    }
}
