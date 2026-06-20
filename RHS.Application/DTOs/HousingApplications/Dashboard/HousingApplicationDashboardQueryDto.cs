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
    }
}
