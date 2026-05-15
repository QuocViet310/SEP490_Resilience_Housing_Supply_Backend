using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.Auth;

public class VerifyOtpDto
{
    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mã OTP là bắt buộc")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có 6 ký tự")]
    public string OtpCode { get; set; } = string.Empty;
}
