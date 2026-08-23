using RHS.Application.DTOs.HouseholdMember;

namespace RHS.Application.DTOs.CitizenProfile;

/// <summary>
/// DTO chứa toàn bộ dữ liệu kế thừa từ Citizen Profile để tự động điền (Pre-fill) vào form đăng ký NOXH.
/// Giúp người dân không cần nhập lại giấy tờ và thông tin từ đầu.
/// </summary>
public class ApplicationPrefillResponseDto
{
    public Guid ApplicantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string CitizenId { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public bool IsEkycVerified { get; set; }

    public string? Occupation { get; set; }
    public string? WorkPlace { get; set; }
    public string CurrentResidence { get; set; } = string.Empty;
    public string PermanentAddress { get; set; } = string.Empty;

    public string HousingStatus { get; set; } = "NO_HOUSE";
    public string MaritalStatus { get; set; } = "SINGLE";
    public string PriorityGroup { get; set; } = string.Empty;

    public decimal? MonthlyIncome { get; set; }
    public decimal? SpouseMonthlyIncome { get; set; }
    public decimal? AverageHousingAreaPerPerson { get; set; }

    public int HouseholdMembersCount { get; set; }
    public List<HouseholdMemberRequestDto> HouseholdMembers { get; set; } = new();

    /// <summary>Danh sách các giấy tờ đã có trong kho tài liệu có thể tái sử dụng</summary>
    public List<PrefillDocumentItemDto> AvailableVaultDocuments { get; set; } = new();
}

public class PrefillDocumentItemDto
{
    public Guid DocumentId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentTypeLabel { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
}
