using System.ComponentModel.DataAnnotations;
using RHS.Domain.Constants;

namespace RHS.Application.DTOs.CitizenProfile;

/// <summary>
/// Request DTO để thêm hoặc cập nhật thành viên trong sổ hộ khẩu / gia đình của công dân.
/// </summary>
public class UserHouseholdMemberRequestDto
{
    [Required(ErrorMessage = "Họ tên thành viên là bắt buộc.")]
    [MaxLength(100, ErrorMessage = "Họ tên không được quá 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Số CCCD/CMND (9 hoặc 12 số). Bắt buộc nếu thành viên từ 14 tuổi trở lên.
    /// </summary>
    [RegularExpression(@"^\d{9}(\d{3})?$", ErrorMessage = "Số CCCD phải là 9 hoặc 12 chữ số.")]
    public string? CitizenId { get; set; }

    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// Quan hệ với chủ hộ: SPOUSE, CHILD, PARENT, SIBLING, GRANDPARENT, GRANDCHILD, OTHER
    /// </summary>
    [Required(ErrorMessage = "Quan hệ với chủ hộ là bắt buộc.")]
    [MaxLength(50, ErrorMessage = "Quan hệ không được quá 50 ký tự.")]
    public string Relationship { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Nghề nghiệp không được quá 200 ký tự.")]
    public string? Occupation { get; set; }

    [Range(0, 1_000_000_000, ErrorMessage = "Thu nhập không hợp lệ.")]
    public decimal? MonthlyIncome { get; set; }

    /// <summary>Đánh dấu là người phụ thuộc (con dưới 18 tuổi, sinh viên, người mất sức lao động)</summary>
    public bool IsDependent { get; set; } = false;

    /// <summary>Lý do người phụ thuộc: UNDER_18, STUDENT, DISABLED, ELDERLY, OTHER</summary>
    public string? DependentReason { get; set; }

    /// <summary>Đánh dấu thành viên có công với cách mạng hoặc thân nhân liệt sĩ</summary>
    public bool HasMeritService { get; set; } = false;

    [MaxLength(500, ErrorMessage = "Chi tiết người có công không quá 500 ký tự.")]
    public string? MeritDetails { get; set; }

    [MaxLength(500, ErrorMessage = "Ghi chú không được quá 500 ký tự.")]
    public string? Note { get; set; }
}

/// <summary>
/// Response DTO trả về thông tin thành viên hộ gia đình trong Profile.
/// </summary>
public class UserHouseholdMemberResponseDto
{
    public Guid MemberId { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? CitizenId { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string Relationship { get; set; } = string.Empty;
    public string? Occupation { get; set; }
    public decimal? MonthlyIncome { get; set; }
    public bool IsDependent { get; set; }
    public string? DependentReason { get; set; }
    public string? DependentReasonLabel { get; set; }
    public bool HasMeritService { get; set; }
    public string? MeritDetails { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
