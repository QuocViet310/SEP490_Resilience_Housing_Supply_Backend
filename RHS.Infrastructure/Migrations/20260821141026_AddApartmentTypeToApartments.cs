using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApartmentTypeToApartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HousingApplications_ApplicantId_ProjectId",
                table: "HousingApplications");

            migrationBuilder.AddColumn<string>(
                name: "ApartmentType",
                table: "Apartments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "TWO_BEDROOM");

            migrationBuilder.CreateIndex(
                name: "IX_HousingApplications_ApplicantId_ProjectId",
                table: "HousingApplications",
                columns: new[] { "ApplicantId", "ProjectId" },
                unique: true,
                filter: "[ApplicationStatus] <> N'CANCELED' AND [ApplicationStatus] <> N'REJECTED'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HousingApplications_ApplicantId_ProjectId",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "ApartmentType",
                table: "Apartments");

            migrationBuilder.CreateIndex(
                name: "IX_HousingApplications_ApplicantId_ProjectId",
                table: "HousingApplications",
                columns: new[] { "ApplicantId", "ProjectId" },
                unique: true,
                filter: "[ApplicationStatus] NOT IN (N'CANCELED', N'REJECTED')");
        }
    }
}
