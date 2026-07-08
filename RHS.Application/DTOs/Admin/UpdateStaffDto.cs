using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.Admin;

/// <summary>
/// DTO để Admin cập nhật thông tin cán bộ
/// </summary>
public class UpdateStaffDto
{
    [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự")]
    public string? FullName { get; set; }

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [StringLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự")]
    public string? PhoneNumber { get; set; }

    [StringLength(20, ErrorMessage = "Số CMND/CCCD không được vượt quá 20 ký tự")]
    public string? CitizenId { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [StringLength(500, ErrorMessage = "Địa chỉ không được vượt quá 500 ký tự")]
    public string? Address { get; set; }

    [RegularExpression(@"^(Active|Inactive|Suspended)$",
        ErrorMessage = "Trạng thái phải là 'Active', 'Inactive' hoặc 'Suspended'")]
    public string? Status { get; set; }

    [RegularExpression(@"^(Department Of Construction|Housing Developer)$",
        ErrorMessage = "Vai trò phải là 'Department Of Construction' hoặc 'Housing Developer'")]
    public string? Role { get; set; }
}
