using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceApartmentTypesWithApartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HousingApplications_ApartmentTypes_ApartmentTypeId",
                table: "HousingApplications");

            migrationBuilder.DropTable(
                name: "ApartmentTypes");

            migrationBuilder.RenameColumn(
                name: "ApartmentTypeId",
                table: "HousingApplications",
                newName: "ApartmentId");

            migrationBuilder.RenameIndex(
                name: "IX_HousingApplications_ApartmentTypeId",
                table: "HousingApplications",
                newName: "IX_HousingApplications_ApartmentId");

            migrationBuilder.AlterColumn<string>(
                name: "LotteryDescription",
                table: "HousingProjects",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Apartments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Area = table.Column<double>(type: "float", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "AVAILABLE"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Apartments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Apartments_HousingProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "HousingProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_ProjectId",
                table: "Apartments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_ProjectId_Status",
                table: "Apartments",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_HousingApplications_Apartments_ApartmentId",
                table: "HousingApplications",
                column: "ApartmentId",
                principalTable: "Apartments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HousingApplications_Apartments_ApartmentId",
                table: "HousingApplications");

            migrationBuilder.DropTable(
                name: "Apartments");

            migrationBuilder.RenameColumn(
                name: "ApartmentId",
                table: "HousingApplications",
                newName: "ApartmentTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_HousingApplications_ApartmentId",
                table: "HousingApplications",
                newName: "IX_HousingApplications_ApartmentTypeId");

            migrationBuilder.AlterColumn<string>(
                name: "LotteryDescription",
                table: "HousingProjects",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ApartmentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Area = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    RemainingQuantity = table.Column<int>(type: "int", nullable: false),
                    TypeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApartmentTypes_HousingProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "HousingProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentTypes_ProjectId",
                table: "ApartmentTypes",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_HousingApplications_ApartmentTypes_ApartmentTypeId",
                table: "HousingApplications",
                column: "ApartmentTypeId",
                principalTable: "ApartmentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
