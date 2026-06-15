using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHousingApplicationFormFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CitizenId",
                table: "HousingApplications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "HousingApplications",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CurrentResidence",
                table: "HousingApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "HousingApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HousingStatus",
                table: "HousingApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Occupation",
                table: "HousingApplications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PermanentAddress",
                table: "HousingApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "HousingApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkPlace",
                table: "HousingApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "ApplicationStatusHistories",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "ApplicationStatusHistories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "VerificationStatus",
                table: "ApplicationDocuments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "PENDING",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "ApplicationDocuments",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "ApplicationDocuments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "UploadedBy",
                table: "ApplicationDocuments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_HousingApplications_CitizenId",
                table: "HousingApplications",
                column: "CitizenId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatusHistories_ApplicationId_ChangedAt",
                table: "ApplicationStatusHistories",
                columns: new[] { "ApplicationId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDocuments_DocumentType",
                table: "ApplicationDocuments",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDocuments_UploadedBy",
                table: "ApplicationDocuments",
                column: "UploadedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationDocuments_Users_UploadedBy",
                table: "ApplicationDocuments",
                column: "UploadedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationDocuments_Users_UploadedBy",
                table: "ApplicationDocuments");

            migrationBuilder.DropIndex(
                name: "IX_HousingApplications_CitizenId",
                table: "HousingApplications");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationStatusHistories_ApplicationId_ChangedAt",
                table: "ApplicationStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationDocuments_DocumentType",
                table: "ApplicationDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationDocuments_UploadedBy",
                table: "ApplicationDocuments");

            migrationBuilder.DropColumn(
                name: "CitizenId",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "CurrentResidence",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "HousingStatus",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "Occupation",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "PermanentAddress",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "WorkPlace",
                table: "HousingApplications");

            migrationBuilder.DropColumn(
                name: "Action",
                table: "ApplicationStatusHistories");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "ApplicationDocuments");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "ApplicationDocuments");

            migrationBuilder.DropColumn(
                name: "UploadedBy",
                table: "ApplicationDocuments");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "ApplicationStatusHistories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VerificationStatus",
                table: "ApplicationDocuments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "PENDING");
        }
    }
}
