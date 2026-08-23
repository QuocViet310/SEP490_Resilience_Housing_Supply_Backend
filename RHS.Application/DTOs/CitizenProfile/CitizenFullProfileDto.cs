namespace RHS.Application.DTOs.CitizenProfile;

/// <summary>
/// Response DTO trả về toàn bộ thông tin Hồ sơ cá nhân của công dân, bao gồm:
/// - Thông tin định danh eKYC
/// - Thông tin hôn nhân & vợ/chồng
/// - Thông tin thu nhập, việc làm & nơi ở
/// - Thực trạng nhà ở & nhóm đối tượng ưu tiên
/// - Danh sách nhân khẩu hộ gia đình đã lưu
/// - Danh sách tài liệu trong kho lưu trữ cá nhân
/// </summary>
public class CitizenFullProfileDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? CitizenId { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }

    // ── eKYC Status ──────────────────────────────────────────────
    public bool IsEkycVerified { get; set; }
    public DateTime? EkycVerifiedAt { get; set; }
    public string? Gender { get; set; }
    public string? Nationality { get; set; }
    public string? PlaceOfOrigin { get; set; }
    public DateTime? IdIssueDate { get; set; }
    public string? IdIssuePlace { get; set; }

    // ── Hôn nhân & Vợ/Chồng ───────────────────────────────────────
    public string? MaritalStatus { get; set; }
    public string? MaritalStatusLabel { get; set; }
    public string? SpouseFullName { get; set; }
    public string? SpouseCitizenId { get; set; }
    public DateTime? SpouseDateOfBirth { get; set; }
    public decimal? SpouseMonthlyIncome { get; set; }

    // ── Việc làm, Nơi ở & Thu nhập ────────────────────────────────
    public string? Occupation { get; set; }
    public string? WorkPlace { get; set; }
    public string? CurrentResidence { get; set; }
    public string? PermanentAddress { get; set; }
    public decimal? MonthlyIncome { get; set; }

    // ── Thực trạng nhà ở & Đối tượng ưu tiên ──────────────────────
    public string? HousingStatus { get; set; }
    public decimal? AverageHousingAreaPerPerson { get; set; }
    public string? PriorityGroup { get; set; }
    public string? PriorityGroupLabel { get; set; }

    // ── Collections ──────────────────────────────────────────────
    public int HouseholdMembersCount { get; set; }
    public List<UserHouseholdMemberResponseDto> HouseholdMembers { get; set; } = new();
    public List<UserDocumentResponseDto> Documents { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
