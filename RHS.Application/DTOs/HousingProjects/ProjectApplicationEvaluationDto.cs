namespace RHS.Application.DTOs.HousingProjects;

/// <summary>
/// Thống kê phân tích danh sách hồ sơ đủ điều kiện (từ SXD) so với số căn có sẵn của dự án
/// giúp Chủ dự án (CĐT) đưa ra quyết định quy trình.
/// </summary>
public class ProjectApplicationEvaluationDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int AvailableUnits { get; set; }
    public int TotalQualifiedApplications { get; set; }
    public int PriorityCount { get; set; }
    public int NonPriorityCount { get; set; }

    /// <summary>
    /// Kịch bản đề xuất: "LESS_OR_EQUAL_AVAILABLE" hoặc "GREATER_THAN_AVAILABLE"
    /// </summary>
    public string RecommendedScenario { get; set; } = string.Empty;

    public List<ApplicationSummaryItemDto> PriorityApplications { get; set; } = new();
    public List<ApplicationSummaryItemDto> NonPriorityApplications { get; set; } = new();
}

public class ApplicationSummaryItemDto
{
    public Guid ApplicationId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string CitizenId { get; set; } = string.Empty;
    public string? PriorityGroup { get; set; }
    public decimal PriorityScore { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string ApplicationStatus { get; set; } = string.Empty;
}
