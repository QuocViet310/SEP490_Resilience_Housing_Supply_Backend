using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RHS.Infrastructure.Data;

#nullable disable

namespace RHS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260724030000_AddLotterySessionFsmAndJoinCode")]
public partial class AddLotterySessionFsmAndJoinCode : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LotteryJoinCode",
            table: "HousingProjects",
            type: "nvarchar(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LotterySessionStatus",
            table: "HousingProjects",
            type: "nvarchar(30)",
            maxLength: 30,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LotteryJoinCode",
            table: "HousingProjects");

        migrationBuilder.DropColumn(
            name: "LotterySessionStatus",
            table: "HousingProjects");
    }
}
