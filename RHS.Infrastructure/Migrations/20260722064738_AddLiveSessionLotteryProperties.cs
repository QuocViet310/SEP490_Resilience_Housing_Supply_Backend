using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveSessionLotteryProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLotteryApproved",
                table: "HousingProjects",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LotteryApprovedAt",
                table: "HousingProjects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LotteryApprovedBy",
                table: "HousingProjects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LotteryDescription",
                table: "HousingProjects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LotteryType",
                table: "HousingProjects",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLotteryApproved",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "LotteryApprovedAt",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "LotteryApprovedBy",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "LotteryDescription",
                table: "HousingProjects");

            migrationBuilder.DropColumn(
                name: "LotteryType",
                table: "HousingProjects");
        }
    }
}
