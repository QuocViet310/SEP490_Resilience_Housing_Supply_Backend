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

    public string? SlotCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public PrincipleAgreement? PrincipleAgreement { get; set; }

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

    public string? MaritalStatus { get; set; }
    public int HouseholdMembersCount { get; set; }
    public string? PriorityGroup { get; set; }
    public string? ReceiptUrl { get; set; }

    /// <summary>Thu nhập tháng thực nhận của người đứng đơn (Đ30).</summary>
    public decimal? MonthlyIncome { get; set; }

    /// <summary>Thu nhập tháng của vợ/chồng (nếu đã kết hôn).</summary>
    public decimal? SpouseMonthlyIncome { get; set; }

    /// <summary>Diện tích nhà ở bình quân đầu người (m²) — dùng khi SMALL_HOUSE (Đ29.2).</summary>
    public decimal? AverageHousingAreaPerPerson { get; set; }

    /// <summary>Kết quả bốc thăm: PENDING / WON / LOST / PRIORITY_WON.</summary>
    public string? LotteryResult { get; set; }

    public Guid? LatestAssessmentId { get; set; }

    /// <summary>Đánh dấu hồ sơ vi phạm (gian lận đất đai). Khi gắn cờ, hồ sơ bị loại khỏi danh sách bốc thăm.</summary>
    public bool IsViolation { get; set; } = false;

    /// <summary>Lý do gắn cờ vi phạm</summary>
    public string? ViolationReason { get; set; }

    /// <summary>Loại căn hộ được phân sau bốc thăm trúng</summary>
    public Guid? ApartmentTypeId { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Navigation properties
    // ─────────────────────────────────────────────────────────────

    public User Applicant { get; set; } = null!;

    public User? Officer { get; set; }

    public HousingProject HousingProject { get; set; } = null!;

    public ApartmentType? ApartmentType { get; set; }

    public ICollection<ApplicationDocument> Documents { get; set; }
        = new List<ApplicationDocument>();

    public ICollection<ApplicationStatusHistory> StatusHistories { get; set; }
        = new List<ApplicationStatusHistory>();

    public ICollection<Appointment> Appointments { get; set; }
        = new List<Appointment>();

    public ICollection<PaymentInstallment> PaymentInstallments { get; set; }
        = new List<PaymentInstallment>();
}
