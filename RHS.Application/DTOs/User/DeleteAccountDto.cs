using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.User;

public class DeleteAccountDto
{
    [Required(ErrorMessage = "Mật khẩu là bắt buộc để xác nhận xóa tài khoản")]
    public string Password { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự")]
    public string? Reason { get; set; }
}
