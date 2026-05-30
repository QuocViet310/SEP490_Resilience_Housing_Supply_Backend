using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.Admin;
using RHS.Application.Interfaces;
using System.Security.Claims;

namespace RHS.API.Controllers;

/// <summary>
/// Controller để quản lý cán bộ (Ward Manager & Verification Officer)
/// Chỉ System Administrator mới có thể truy cập các endpoint này
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>
    /// Admin tạo tài khoản cán bộ mới (Ward Manager hoặc Verification Officer)
    /// </summary>
    /// <param name="createStaffDto">Thông tin cán bộ cần tạo</param>
    /// <returns>Thông tin cán bộ vừa được tạo</returns>
    [HttpPost("create-staff")]
    [ProducesResponseType(typeof(StaffResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffDto createStaffDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _adminService.CreateStaffAsync(createStaffDto, adminId);
            return CreatedAtAction(nameof(GetStaffById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy danh sách cán bộ với phân trang và bộ lọc
    /// </summary>
    /// <param name="queryDto">Tham số truy vấn (phân trang, bộ lọc, tìm kiếm)</param>
    /// <returns>Danh sách cán bộ</returns>
    [HttpGet("staff-list")]
    [ProducesResponseType(typeof(StaffListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStaffList([FromQuery] GetStaffListDto queryDto)
    {
        try
        {
            var result = await _adminService.GetStaffListAsync(queryDto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy thông tin chi tiết một cán bộ
    /// </summary>
    /// <param name="id">ID của cán bộ</param>
    /// <returns>Thông tin chi tiết cán bộ</returns>
    [HttpGet("staff/{id:guid}")]
    [ProducesResponseType(typeof(StaffResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStaffById(Guid id)
    {
        try
        {
            var staff = await _adminService.GetStaffByIdAsync(id);
            if (staff == null)
                return NotFound(new { success = false, message = $"Không tìm thấy cán bộ với ID {id}" });

            return Ok(staff);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Admin cập nhật thông tin cán bộ
    /// </summary>
    /// <param name="id">ID của cán bộ</param>
    /// <param name="updateStaffDto">Thông tin cập nhật</param>
    /// <returns>Thông tin cán bộ sau cập nhật</returns>
    [HttpPut("staff/{id:guid}")]
    [ProducesResponseType(typeof(StaffResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateStaff(Guid id, [FromBody] UpdateStaffDto updateStaffDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _adminService.UpdateStaffAsync(id, updateStaffDto, adminId);
            
            if (result == null)
                return NotFound(new { success = false, message = $"Không tìm thấy cán bộ với ID {id}" });

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Admin phân quyền cho cán bộ (thay đổi vai trò/trạng thái)
    /// </summary>
    /// <param name="assignPermissionDto">Thông tin phân quyền</param>
    /// <returns>Thông tin cán bộ sau khi phân quyền</returns>
    [HttpPost("assign-permission")]
    [ProducesResponseType(typeof(StaffResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AssignPermission([FromBody] AssignPermissionDto assignPermissionDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _adminService.AssignPermissionAsync(assignPermissionDto, adminId);
            
            if (result == null)
                return BadRequest(new { success = false, message = "Không thể phân quyền" });

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Admin khóa tài khoản cán bộ
    /// </summary>
    /// <param name="id">ID của cán bộ</param>
    /// <param name="reason">Lý do khóa tài khoản</param>
    /// <returns>Kết quả thao tác</returns>
    [HttpPost("staff/{id:guid}/deactivate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeactivateStaff(Guid id, [FromBody] string? reason = null)
    {
        try
        {
            var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _adminService.DeactivateStaffAsync(id, reason ?? "No reason provided", adminId);

            return Ok(new { success = result, message = "Tài khoản cán bộ đã được khóa" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Admin kích hoạt lại tài khoản cán bộ
    /// </summary>
    /// <param name="id">ID của cán bộ</param>
    /// <returns>Kết quả thao tác</returns>
    [HttpPost("staff/{id:guid}/activate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ActivateStaff(Guid id)
    {
        try
        {
            var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _adminService.ActivateStaffAsync(id, adminId);

            return Ok(new { success = result, message = "Tài khoản cán bộ đã được kích hoạt" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Admin đặt lại mật khẩu cho cán bộ
    /// </summary>
    /// <param name="id">ID của cán bộ</param>
    /// <param name="resetPasswordDto">Mật khẩu mới</param>
    /// <returns>Kết quả thao tác</returns>
    [HttpPost("staff/{id:guid}/reset-password")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequestDto resetPasswordDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _adminService.ResetStaffPasswordAsync(id, resetPasswordDto.NewPassword, adminId);

            return Ok(new { success = result, message = "Mật khẩu đã được đặt lại thành công" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}

/// <summary>
/// Helper DTO cho API reset password
/// </summary>
public class ResetPasswordRequestDto
{
    public string NewPassword { get; set; } = string.Empty;
}
