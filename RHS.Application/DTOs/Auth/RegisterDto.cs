using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.Auth;

public class RegisterDto
{
    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ tên là bắt buộc")]
    [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    public string? PhoneNumber { get; set; }

    [StringLength(20, ErrorMessage = "Số CMND/CCCD không được vượt quá 20 ký tự")]
    public string? CitizenId { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [StringLength(500, ErrorMessage = "Địa chỉ không được vượt quá 500 ký tự")]
    public string? Address { get; set; }

    [Required(ErrorMessage = "Vai trò là bắt buộc")]
    public string Role { get; set; } = "Applicant";
}
