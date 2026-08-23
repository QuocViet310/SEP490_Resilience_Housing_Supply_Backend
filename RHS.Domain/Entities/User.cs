namespace RHS.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? PasswordHash { get; set; }
    public string? CitizenId { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = "Active"; // Active, Inactive, Suspended
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── eKYC & Định danh công dân ────────────────────────────────
    public bool IsEkycVerified { get; set; } = false;
    public DateTime? EkycVerifiedAt { get; set; }
    public string? Gender { get; set; }
    public string? Nationality { get; set; }
    public string? PlaceOfOrigin { get; set; }       // Quê quán
    public DateTime? IdIssueDate { get; set; }        // Ngày cấp CCCD
    public string? IdIssuePlace { get; set; }        // Nơi cấp CCCD

    // ── Tình trạng hôn nhân & Vợ/Chồng ────────────────────────────
    public string? MaritalStatus { get; set; }        // SINGLE, MARRIED, DIVORCED
    public string? SpouseFullName { get; set; }
    public string? SpouseCitizenId { get; set; }
    public DateTime? SpouseDateOfBirth { get; set; }
    public decimal? SpouseMonthlyIncome { get; set; }

    // ── Nghề nghiệp, Thu nhập & Nơi ở ─────────────────────────────
    public string? Occupation { get; set; }
    public string? WorkPlace { get; set; }
    public string? CurrentResidence { get; set; }    // Nơi ở hiện tại
    public string? PermanentAddress { get; set; }    // Nơi đăng ký thường trú / tạm trú (KT3)
    public decimal? MonthlyIncome { get; set; }

    // ── Thực trạng nhà ở & Đối tượng ưu tiên ──────────────────────
    public string? HousingStatus { get; set; }       // NO_HOUSE, SMALL_HOUSE
    public decimal? AverageHousingAreaPerPerson { get; set; }
    public string? PriorityGroup { get; set; }       // Nhóm đối tượng thụ hưởng mặc định (Đ76)

    // Legacy fields for backward compatibility (will be removed later)
    public bool IsEmailVerified { get; set; }
    public string? GoogleId { get; set; }
    public string? ProfileImageUrl { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Navigation properties
    public Role Role { get; set; } = null!;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<OtpVerification> OtpVerifications { get; set; } = new List<OtpVerification>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    // New Navigation Properties
    public ICollection<HousingApplication> HousingApplications { get; set; } = new List<HousingApplication>();
    public ICollection<HousingApplication> AssignedApplications { get; set; } = new List<HousingApplication>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<EligibilityAssessment> EligibilityAssessments { get; set; } = new List<EligibilityAssessment>();
    public ICollection<Message> SentMessages { get; set; } = new List<Message>();
    public ICollection<Message> ReceivedMessages { get; set; } = new List<Message>();
    public ICollection<IssueReport> IssueReports { get; set; } = new List<IssueReport>();
    public ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
    public ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();

    // Reusable Citizen Profile Collections
    public ICollection<UserHouseholdMember> UserHouseholdMembers { get; set; } = new List<UserHouseholdMember>();
    public ICollection<UserDocument> UserDocuments { get; set; } = new List<UserDocument>();
}
