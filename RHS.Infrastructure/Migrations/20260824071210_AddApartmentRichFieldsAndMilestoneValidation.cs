using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApartmentRichFieldsAndMilestoneValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BalconyDirection",
                table: "Apartments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildingBlock",
                table: "Apartments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CoOwnershipRatio",
                table: "Apartments",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FloorNumber",
                table: "Apartments",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<double>(
                name: "GrossArea",
                table: "Apartments",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MainDoorDirection",
                table: "Apartments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxOccupants",
                table: "Apartments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxSuitableIncome",
                table: "Apartments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinSuitableIncome",
                table: "Apartments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfBathrooms",
                table: "Apartments",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfBedrooms",
                table: "Apartments",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "SaleType",
                table: "Apartments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FULL_OWNERSHIP");

            migrationBuilder.AddColumn<string>(
                name: "UnitGroup",
                table: "Apartments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "STANDARD");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Apartments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViewDescription",
                table: "Apartments",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_ProjectId_BuildingBlock",
                table: "Apartments",
                columns: new[] { "ProjectId", "BuildingBlock" });

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_ProjectId_FloorNumber",
                table: "Apartments",
                columns: new[] { "ProjectId", "FloorNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_ProjectId_SaleType",
                table: "Apartments",
                columns: new[] { "ProjectId", "SaleType" });

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_ProjectId_UnitGroup",
                table: "Apartments",
                columns: new[] { "ProjectId", "UnitGroup" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Apartments_ProjectId_BuildingBlock",
                table: "Apartments");

            migrationBuilder.DropIndex(
                name: "IX_Apartments_ProjectId_FloorNumber",
                table: "Apartments");

            migrationBuilder.DropIndex(
                name: "IX_Apartments_ProjectId_SaleType",
                table: "Apartments");

            migrationBuilder.DropIndex(
                name: "IX_Apartments_ProjectId_UnitGroup",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "BalconyDirection",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "BuildingBlock",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "CoOwnershipRatio",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "FloorNumber",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "GrossArea",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "MainDoorDirection",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "MaxOccupants",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "MaxSuitableIncome",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "MinSuitableIncome",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "NumberOfBathrooms",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "NumberOfBedrooms",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "SaleType",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "UnitGroup",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "ViewDescription",
                table: "Apartments");
        }
    }
}
