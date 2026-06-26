using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationIdToPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ApplicationId",
                table: "Payments",
                column: "ApplicationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_HousingApplications_ApplicationId",
                table: "Payments",
                column: "ApplicationId",
                principalTable: "HousingApplications",
                principalColumn: "ApplicationId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_HousingApplications_ApplicationId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ApplicationId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "Payments");
        }
    }
}
