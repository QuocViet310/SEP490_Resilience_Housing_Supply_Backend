using System;

namespace RHS.Application.DTOs.HousingApplications.Dashboard
{
    public class HousingApplicationDashboardItemDto
    {
        public Guid ApplicationId { get; set; }

        public string ApplicantName { get; set; } = string.Empty;

        public string ApplicantEmail { get; set; } = string.Empty;

        public string ProjectName { get; set; } = string.Empty;

        public string ApplicationStatus { get; set; } = string.Empty;

        public decimal PriorityScore { get; set; }

        public decimal EstimatedMonthlyIncome { get; set; }

        public DateTime SubmittedAt { get; set; }
    }
}
