using System;

namespace RHS.Application.DTOs.HousingApplications.Dashboard
{
    public class HousingApplicationDashboardItemDto
    {
        public Guid ApplicationId { get; set; }

        public Guid ProjectId { get; set; }

        /// <summary>Legacy field — keep for older clients.</summary>
        public string ApplicantName { get; set; } = string.Empty;

        /// <summary>Aligned with ApplicationSummaryResponseDto for web list UI.</summary>
        public string ApplicantFullName { get; set; } = string.Empty;

        public string CitizenId { get; set; } = string.Empty;

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
