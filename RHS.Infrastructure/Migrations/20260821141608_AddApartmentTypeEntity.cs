using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApartmentTypeEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApartmentType",
                table: "Apartments");

            migrationBuilder.AddColumn<Guid>(
                name: "ApartmentTypeId",
                table: "Apartments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApartmentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TypeCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TypeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_ApartmentTypeId",
                table: "Apartments",
                column: "ApartmentTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Apartments_ApartmentTypes_ApartmentTypeId",
                table: "Apartments",
                column: "ApartmentTypeId",
                principalTable: "ApartmentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Apartments_ApartmentTypes_ApartmentTypeId",
                table: "Apartments");

            migrationBuilder.DropTable(
                name: "ApartmentTypes");

            migrationBuilder.DropIndex(
                name: "IX_Apartments_ApartmentTypeId",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "ApartmentTypeId",
                table: "Apartments");

            migrationBuilder.AddColumn<string>(
                name: "ApartmentType",
                table: "Apartments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "TWO_BEDROOM");
        }
    }
}
