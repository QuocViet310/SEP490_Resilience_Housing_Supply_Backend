using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.HouseholdMember;

/// <summary>
/// Request DTO cho thêm/cập nhật thành viên hộ gia đình.
/// CitizenId bắt buộc nếu thành viên ≥ 14 tuổi (validate phía server dựa trên DateOfBirth).
/// </summary>
public class HouseholdMemberRequestDto
{
    /// <summary>Họ và tên thành viên</summary>
    [Required(ErrorMessage = "Họ tên thành viên là bắt buộc.")]
    [MaxLength(100, ErrorMessage = "Họ tên không được quá 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Số CCCD/CMND (9 hoặc 12 số).
    /// Bắt buộc nếu thành viên từ 14 tuổi trở lên.
    /// </summary>
    [RegularExpression(@"^\d{9}(\d{3})?$", ErrorMessage = "Số CCCD phải là 9 hoặc 12 chữ số.")]
    public string? CitizenId { get; set; }

    /// <summary>Ngày sinh của thành viên</summary>
    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// Quan hệ với người đứng đơn.
    /// Giá trị hợp lệ: SPOUSE, CHILD, PARENT, SIBLING, GRANDPARENT, GRANDCHILD, OTHER
    /// </summary>
    [Required(ErrorMessage = "Quan hệ với chủ hộ là bắt buộc.")]
    [MaxLength(50, ErrorMessage = "Quan hệ không được quá 50 ký tự.")]
    public string Relationship { get; set; } = string.Empty;

    /// <summary>Ghi chú bổ sung</summary>
    [MaxLength(500, ErrorMessage = "Ghi chú không được quá 500 ký tự.")]
    public string? Note { get; set; }
}

/// <summary>
/// Response DTO trả về thông tin thành viên hộ gia đình.
/// </summary>
public class HouseholdMemberResponseDto
{
    public Guid MemberId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? CitizenId { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string Relationship { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
