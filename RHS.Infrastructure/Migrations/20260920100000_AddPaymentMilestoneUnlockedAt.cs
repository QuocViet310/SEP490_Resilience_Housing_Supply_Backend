using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RHS.Infrastructure.Data;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260920100000_AddPaymentMilestoneUnlockedAt")]
    public partial class AddPaymentMilestoneUnlockedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UnlockedAt",
                table: "PaymentMilestones",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnlockedAt",
                table: "PaymentMilestones");
        }
    }
}
