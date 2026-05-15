using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.Auth;

public class GoogleLoginDto
{
    [Required(ErrorMessage = "Google ID Token là bắt buộc")]
    public string GoogleIdToken { get; set; } = string.Empty;

    public string Role { get; set; } = "Applicant";
}
