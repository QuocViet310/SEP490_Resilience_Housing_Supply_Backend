using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.User;

public class UpdateProfileDto
{
    [Required(ErrorMessage = "Họ và tên là bắt buộc")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ và tên phải có từ 2 đến 100 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [StringLength(15, ErrorMessage = "Số điện thoại không được vượt quá 15 ký tự")]
    public string? PhoneNumber { get; set; }
}
