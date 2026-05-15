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
