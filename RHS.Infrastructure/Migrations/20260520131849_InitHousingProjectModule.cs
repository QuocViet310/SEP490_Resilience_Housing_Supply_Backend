using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitHousingProjectModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HousingProjectStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StatusCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousingProjectStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HousingProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MinPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinArea = table.Column<double>(type: "float", nullable: false),
                    MaxArea = table.Column<double>(type: "float", nullable: false),
                    AvailableUnits = table.Column<int>(type: "int", nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HousingProjectStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousingProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HousingProjects_HousingProjectStatuses_HousingProjectStatusId",
                        column: x => x.HousingProjectStatusId,
                        principalTable: "HousingProjectStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HousingProjects_District",
                table: "HousingProjects",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "IX_HousingProjects_HousingProjectStatusId",
                table: "HousingProjects",
                column: "HousingProjectStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_HousingProjects_Province",
                table: "HousingProjects",
                column: "Province");

            migrationBuilder.CreateIndex(
                name: "IX_HousingProjectStatuses_StatusCode",
                table: "HousingProjectStatuses",
                column: "StatusCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HousingProjects");

            migrationBuilder.DropTable(
                name: "HousingProjectStatuses");
        }
    }
}
