namespace RHS.Domain.Entities;

/// <summary>
/// Hồ sơ đăng ký nhà ở xã hội của Applicant.
/// Lưu trữ toàn bộ thông tin khai báo trong Form đăng ký.
/// </summary>
public class HousingApplication
{
    public Guid ApplicationId { get; set; }

    public Guid ApplicantId { get; set; }

    public Guid ProjectId { get; set; }

    /// <summary>Officer được giao thẩm định hồ sơ (Verification Officer)</summary>
    public Guid? OfficerId { get; set; }

    /// <summary>
    /// Trạng thái hồ sơ. Sử dụng ApplicationStatusConstants.
    /// Ví dụ: DRAFT, SUBMITTED, UNDER_REVIEW, NEED_MORE_DOCUMENTS, APPROVED, REJECTED
    /// </summary>
    public string ApplicationStatus { get; set; } = string.Empty;

    public DateTime SubmittedAt { get; set; }

    public decimal PriorityScore { get; set; }

    public DateTime? FinalDecisionDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Thông tin Form đăng ký (Application Form Fields)
    // ─────────────────────────────────────────────────────────────

    /// <summary>Họ và tên đầy đủ của người đăng ký</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Số CCCD/CMND của người đăng ký</summary>
    public string CitizenId { get; set; } = string.Empty;

    /// <summary>Nghề nghiệp hiện tại</summary>
    public string? Occupation { get; set; }

    /// <summary>Nơi làm việc (tên cơ quan/công ty)</summary>
    public string? WorkPlace { get; set; }

    /// <summary>Nơi ở hiện tại (địa chỉ thực tế đang sinh sống)</summary>
    public string CurrentResidence { get; set; } = string.Empty;

    /// <summary>Nơi đăng ký thường trú/tạm trú (theo hộ khẩu hoặc KT3)</summary>
    public string PermanentAddress { get; set; } = string.Empty;

    /// <summary>
    /// Thực trạng nhà ở:
    /// "NO_HOUSE" = Chưa có nhà,
    /// "SMALL_HOUSE" = Diện tích nhà ở dưới 15m²
    /// </summary>
    public string HousingStatus { get; set; } = string.Empty;

    /// <summary>Mức thu nhập hàng tháng (VNĐ)</summary>
    public decimal EstimatedMonthlyIncome { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Navigation properties
    // ─────────────────────────────────────────────────────────────

    public User Applicant { get; set; } = null!;

    public User? Officer { get; set; }

    public HousingProject HousingProject { get; set; } = null!;

    public ICollection<ApplicationDocument> Documents { get; set; }
        = new List<ApplicationDocument>();

    public ICollection<ApplicationStatusHistory> StatusHistories { get; set; }
        = new List<ApplicationStatusHistory>();

    public ICollection<Appointment> Appointments { get; set; }
        = new List<Appointment>();
}
