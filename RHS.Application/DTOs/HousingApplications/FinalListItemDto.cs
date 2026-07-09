namespace RHS.Application.DTOs.HousingApplications;

/// <summary>
/// DTO chứa thông tin chi tiết cho danh sách chốt cuối (Final List) của dự án (Task #10).
/// Dữ liệu này dùng để Sở Xây dựng export ra Excel/PDF công bố trên website.
/// Chỉ bao gồm các hồ sơ có trạng thái DEPOSIT_PAID.
/// </summary>
public class FinalListItemDto
{
    // ── Thông tin hồ sơ ─────────────────────────────────
    public Guid ApplicationId { get; set; }
    public string ApplicationStatus { get; set; } = string.Empty;
    public decimal PriorityScore { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? FinalDecisionDate { get; set; }
    public string? SlotCode { get; set; }

    // ── Thông tin người đăng ký ─────────────────────────
    public Guid ApplicantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string CitizenId { get; set; } = string.Empty;
    public string? Occupation { get; set; }
    public string? WorkPlace { get; set; }
    public string CurrentResidence { get; set; } = string.Empty;
    public string PermanentAddress { get; set; } = string.Empty;
    public string HousingStatus { get; set; } = string.Empty;
    public string? MaritalStatus { get; set; }
    public int HouseholdMembersCount { get; set; }
    public string? PriorityGroup { get; set; }

    // ── Thông tin dự án ─────────────────────────────────
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectAddress { get; set; } = string.Empty;
}
