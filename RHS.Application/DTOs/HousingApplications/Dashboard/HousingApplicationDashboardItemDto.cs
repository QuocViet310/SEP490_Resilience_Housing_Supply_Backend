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
        public string? MaritalStatus { get; set; }
        public int HouseholdMembersCount { get; set; }
        public string? PriorityGroup { get; set; }
        public string? ReceiptUrl { get; set; }
        public DateTime SubmittedAt { get; set; }
    }
}
