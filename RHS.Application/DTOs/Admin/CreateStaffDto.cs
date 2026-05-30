using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.Admin;

/// <summary>
/// DTO để Admin tạo tài khoản cán bộ (Ward Manager, Verification Officer)
/// </summary>
public class CreateStaffDto
{
    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ tên là bắt buộc")]
    [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [StringLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự")]
    public string? PhoneNumber { get; set; }

    [StringLength(20, ErrorMessage = "Số CMND/CCCD không được vượt quá 20 ký tự")]
    public string? CitizenId { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [StringLength(500, ErrorMessage = "Địa chỉ không được vượt quá 500 ký tự")]
    public string? Address { get; set; }

    [Required(ErrorMessage = "Vai trò là bắt buộc")]
    [RegularExpression(@"^(Ward Manager|Verification Officer)$", 
        ErrorMessage = "Vai trò phải là 'Ward Manager' hoặc 'Verification Officer'")]
    public string Role { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu tạm thời là bắt buộc")]
    [MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự")]
    public string TemporaryPassword { get; set; } = string.Empty;
}
