using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RHS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniqueApartmentPerActiveApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Trước khi bật unique: nhả căn ở các hồ sơ trùng do trước đây không có ràng buộc nào.
            // Program.cs gọi Migrate() lúc khởi động, nên nếu để index tự vỡ vì dữ liệu cũ thì
            // cả API không lên được. Giữ lại hồ sơ đi xa nhất trong quy trình (đã thanh toán /
            // đã ký trước hồ sơ mới trúng), các hồ sơ còn lại nhả căn để cán bộ gán lại.
            migrationBuilder.Sql(@"
WITH ranked AS (
    SELECT ApplicationId,
           ROW_NUMBER() OVER (
               PARTITION BY ApartmentId
               ORDER BY CASE ApplicationStatus
                            WHEN N'FULLY_PAID'              THEN 1
                            WHEN N'INSTALLMENT_IN_PROGRESS'  THEN 2
                            WHEN N'CONTRACT_SIGNED'          THEN 3
                            WHEN N'DEPOSIT_PAID'             THEN 4
                            WHEN N'CONTRACT_PENDING'         THEN 5
                            WHEN N'DEPOSIT_PENDING'          THEN 6
                            WHEN N'LOTTERY_WON'              THEN 7
                            ELSE 8
                        END,
                        SubmittedAt
           ) AS rn
    FROM HousingApplications
    WHERE ApartmentId IS NOT NULL
      AND ApplicationStatus NOT IN (N'CANCELED', N'REJECTED', N'EXPIRED', N'LOTTERY_LOST', N'WAITLIST')
)
UPDATE HousingApplications
SET ApartmentId = NULL,
    UpdatedAt   = SYSUTCDATETIME()
WHERE ApplicationId IN (SELECT ApplicationId FROM ranked WHERE rn > 1);
");

            migrationBuilder.DropIndex(
                name: "IX_HousingApplications_ApartmentId",
                table: "HousingApplications");

            migrationBuilder.CreateIndex(
                name: "IX_HousingApplications_ApartmentId",
                table: "HousingApplications",
                column: "ApartmentId",
                unique: true,
                filter: "[ApartmentId] IS NOT NULL AND [ApplicationStatus] NOT IN (N'CANCELED', N'REJECTED', N'EXPIRED', N'LOTTERY_LOST', N'WAITLIST')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HousingApplications_ApartmentId",
                table: "HousingApplications");

            migrationBuilder.CreateIndex(
                name: "IX_HousingApplications_ApartmentId",
                table: "HousingApplications",
                column: "ApartmentId");
        }
    }
}
