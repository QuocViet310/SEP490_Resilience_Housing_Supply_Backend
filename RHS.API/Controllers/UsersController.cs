using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.User;
using RHS.Application.Interfaces;
using System.Security.Claims;

namespace RHS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Lấy thông tin profile của user hiện tại
    /// </summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });
        }

        var profile = await _userService.GetProfileAsync(userId);

        if (profile == null)
        {
            return NotFound(new { success = false, message = "Người dùng không tồn tại" });
        }

        return Ok(new
        {
            success = true,
            user = profile
        });
    }

    /// <summary>
    /// Cập nhật thông tin profile của user hiện tại
    /// </summary>
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto updateProfileDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });
        }

        var updatedProfile = await _userService.UpdateProfileAsync(userId, updateProfileDto);

        if (updatedProfile == null)
        {
            return NotFound(new { success = false, message = "Người dùng không tồn tại" });
        }

        return Ok(new
        {
            success = true,
            message = "Cập nhật thông tin thành công",
            user = updatedProfile
        });
    }

    /// <summary>
    /// Upload ảnh đại diện cho user hiện tại
    /// </summary>
    [HttpPost("profile/image")]
    public async Task<IActionResult> UploadProfileImage([FromForm] UploadProfileImageDto uploadDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });
        }

        try
        {
            var updatedProfile = await _userService.UploadProfileImageAsync(userId, uploadDto.Image);

            if (updatedProfile == null)
            {
                return NotFound(new { success = false, message = "Người dùng không tồn tại" });
            }

            return Ok(new
            {
                success = true,
                message = "Upload ảnh đại diện thành công",
                user = updatedProfile
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Xóa ảnh đại diện của user hiện tại
    /// </summary>
    [HttpDelete("profile/image")]
    public async Task<IActionResult> DeleteProfileImage()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });
        }

        var result = await _userService.DeleteProfileImageAsync(userId);

        if (!result)
        {
            return NotFound(new { success = false, message = "Không tìm thấy ảnh đại diện để xóa" });
        }

        return Ok(new
        {
            success = true,
            message = "Xóa ảnh đại diện thành công"
        });
    }

    /// <summary>
    /// Xóa tài khoản của user hiện tại (soft delete)
    /// </summary>
    [HttpPost("delete-account")]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountDto deleteAccountDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });
        }

        var result = await _userService.DeleteAccountAsync(userId, deleteAccountDto.Password, deleteAccountDto.Reason);

        if (!result)
        {
            return BadRequest(new { success = false, message = "Mật khẩu không chính xác hoặc tài khoản không tồn tại" });
        }

        return Ok(new
        {
            success = true,
            message = "Xóa tài khoản thành công. Chúng tôi rất tiếc khi bạn rời đi."
        });
    }

    /// <summary>
    /// Test endpoint chỉ dành cho Admin
    /// </summary>
    [HttpGet("admin-only")]
    [Authorize(Roles = "Admin")]
    public IActionResult AdminOnly()
    {
        return Ok(new
        {
            success = true,
            message = "Chào mừng Admin!"
        });
    }

    /// <summary>
    /// Test endpoint chỉ dành cho Officer
    /// </summary>
    [HttpGet("officer-only")]
    [Authorize(Roles = "Officer")]
    public IActionResult OfficerOnly()
    {
        return Ok(new
        {
            success = true,
            message = "Chào mừng Officer!"
        });
    }
}
