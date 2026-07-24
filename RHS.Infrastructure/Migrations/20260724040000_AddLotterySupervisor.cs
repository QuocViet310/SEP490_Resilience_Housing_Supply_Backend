using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RHS.Infrastructure.Data;

#nullable disable

namespace RHS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260724040000_AddLotterySupervisor")]
public partial class AddLotterySupervisor : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "LotterySupervisorId",
            table: "HousingProjects",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LotterySupervisedAt",
            table: "HousingProjects",
            type: "datetime2",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_HousingProjects_LotterySupervisorId",
            table: "HousingProjects",
            column: "LotterySupervisorId");

        migrationBuilder.AddForeignKey(
            name: "FK_HousingProjects_Users_LotterySupervisorId",
            table: "HousingProjects",
            column: "LotterySupervisorId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_HousingProjects_Users_LotterySupervisorId",
            table: "HousingProjects");

        migrationBuilder.DropIndex(
            name: "IX_HousingProjects_LotterySupervisorId",
            table: "HousingProjects");

        migrationBuilder.DropColumn(
            name: "LotterySupervisorId",
            table: "HousingProjects");

        migrationBuilder.DropColumn(
            name: "LotterySupervisedAt",
            table: "HousingProjects");
    }
}
