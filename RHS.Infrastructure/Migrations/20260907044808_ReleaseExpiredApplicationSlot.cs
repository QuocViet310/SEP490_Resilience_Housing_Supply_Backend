using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReleaseExpiredApplicationSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HousingApplications_ApplicantId_ProjectId",
                table: "HousingApplications");

            migrationBuilder.CreateIndex(
                name: "IX_HousingApplications_ApplicantId_ProjectId",
                table: "HousingApplications",
                columns: new[] { "ApplicantId", "ProjectId" },
                unique: true,
                filter: "[ApplicationStatus] NOT IN (N'CANCELED', N'REJECTED', N'EXPIRED')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HousingApplications_ApplicantId_ProjectId",
                table: "HousingApplications");

            migrationBuilder.CreateIndex(
                name: "IX_HousingApplications_ApplicantId_ProjectId",
                table: "HousingApplications",
                columns: new[] { "ApplicantId", "ProjectId" },
                unique: true,
                filter: "[ApplicationStatus] <> N'CANCELED' AND [ApplicationStatus] <> N'REJECTED'");
        }
    }
}
