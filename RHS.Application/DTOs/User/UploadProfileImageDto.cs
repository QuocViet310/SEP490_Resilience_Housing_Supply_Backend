using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.User;

public class UploadProfileImageDto
{
    [Required(ErrorMessage = "File ảnh là bắt buộc")]
    public IFormFile Image { get; set; } = null!;
}
