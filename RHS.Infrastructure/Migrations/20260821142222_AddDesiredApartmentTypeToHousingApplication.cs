using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDesiredApartmentTypeToHousingApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DesiredApartmentTypeId",
                table: "HousingApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HousingApplications_DesiredApartmentTypeId",
                table: "HousingApplications",
                column: "DesiredApartmentTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_HousingApplications_ApartmentTypes_DesiredApartmentTypeId",
                table: "HousingApplications",
                column: "DesiredApartmentTypeId",
                principalTable: "ApartmentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HousingApplications_ApartmentTypes_DesiredApartmentTypeId",
                table: "HousingApplications");

            migrationBuilder.DropIndex(
                name: "IX_HousingApplications_DesiredApartmentTypeId",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "DesiredApartmentTypeId",
                table: "HousingApplications");
        }
    }
}
