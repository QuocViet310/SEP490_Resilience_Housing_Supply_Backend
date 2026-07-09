using System;

namespace RHS.Application.DTOs.HousingApplications.Dashboard
{
    public class HousingApplicationDashboardQueryDto
    {
        public int PageIndex { get; set; } = 1;

        public int PageSize { get; set; } = 10;

        public string? Search { get; set; }

        public Guid? ProjectId { get; set; }

        public string? Status { get; set; }

        /// <summary>
        /// ID của Housing Developer (CĐT) đang đăng nhập.
        /// Dùng để filter chỉ hiển thị hồ sơ thuộc dự án của CĐT này.
        /// Được set tự động từ JWT, không phải từ query string.
        /// </summary>
        public Guid? DeveloperId { get; set; }
    }
}
