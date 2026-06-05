using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LinkPaymentToHousingProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HousingProjectId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_HousingProjectId",
                table: "Payments",
                column: "HousingProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_HousingProjects_HousingProjectId",
                table: "Payments",
                column: "HousingProjectId",
                principalTable: "HousingProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_HousingProjects_HousingProjectId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_HousingProjectId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "HousingProjectId",
                table: "Payments");
        }
    }
}
