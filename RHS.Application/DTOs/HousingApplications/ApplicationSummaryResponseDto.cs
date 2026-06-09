namespace RHS.Application.DTOs.HousingApplications;

/// <summary>
/// Response DTO cho danh sách hồ sơ (tóm tắt).
/// Dùng trong API lấy danh sách có phân trang.
/// </summary>
public class ApplicationSummaryResponseDto
{
    public Guid ApplicationId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;

    // Thông tin người đăng ký
    public Guid ApplicantId { get; set; }
    public string ApplicantFullName { get; set; } = string.Empty;
    public string CitizenId { get; set; } = string.Empty;

    // Trạng thái & thời gian
    public string ApplicationStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? FinalDecisionDate { get; set; }

    // Thông tin nhanh
    public string HousingStatus { get; set; } = string.Empty;
    public decimal EstimatedMonthlyIncome { get; set; }
    public int DocumentCount { get; set; }
}
