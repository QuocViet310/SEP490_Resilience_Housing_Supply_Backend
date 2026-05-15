using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.Auth;

public class RefreshTokenDto
{
    [Required(ErrorMessage = "Refresh token là bắt buộc")]
    public string RefreshToken { get; set; } = string.Empty;
}
