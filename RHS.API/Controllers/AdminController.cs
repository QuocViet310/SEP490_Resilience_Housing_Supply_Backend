using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.Admin;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using System.Security.Claims;

namespace RHS.API.Controllers;

/// <summary>
/// Controller cho Super Admin quản lý hệ thống cấp cao:
/// 1. Cấp quyền & Quản lý tài khoản CĐT / Sở Xây dựng
/// 2. Nhật ký kiểm toán (Audit Trail)
/// 3. Báo cáo thống kê toàn sàn (Overview, Hấp thụ, Giải ngân, Tỷ lệ hồ sơ)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleConstants.SystemAdministrator)]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IAuditLogService _auditLogService;
    private readonly ISuperAdminDashboardService _dashboardService;

    public AdminController(
        IAdminService adminService,
        IAuditLogService auditLogService,
        ISuperAdminDashboardService dashboardService)
    {
        _adminService = adminService;
        _auditLogService = auditLogService;
        _dashboardService = dashboardService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1. Quản lý cán bộ & Cấp quyền (Housing Developer & SXD)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Admin tạo tài khoản cán bộ mới (Department Of Construction hoặc Housing Developer)</summary>
    [HttpPost("create-staff")]
    [ProducesResponseType(typeof(StaffResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
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

    /// <summary>Lấy danh sách cán bộ với phân trang và bộ lọc</summary>
    [HttpGet("staff-list")]
    [ProducesResponseType(typeof(StaffListResponseDto), StatusCodes.Status200OK)]
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

    /// <summary>Lấy thông tin chi tiết một cán bộ</summary>
    [HttpGet("staff/{id:guid}")]
    [ProducesResponseType(typeof(StaffResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
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

    /// <summary>Admin cập nhật thông tin cán bộ</summary>
    [HttpPut("staff/{id:guid}")]
    [ProducesResponseType(typeof(StaffResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
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

    /// <summary>Admin phân quyền cho cán bộ (thay đổi vai trò/trạng thái)</summary>
    [HttpPost("assign-permission")]
    [ProducesResponseType(typeof(StaffResponseDto), StatusCodes.Status200OK)]
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

    /// <summary>Admin khóa tài khoản cán bộ</summary>
    [HttpPost("staff/{id:guid}/deactivate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
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

    /// <summary>Admin kích hoạt lại tài khoản cán bộ</summary>
    [HttpPost("staff/{id:guid}/activate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
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

    /// <summary>Admin đặt lại mật khẩu cho cán bộ</summary>
    [HttpPost("staff/{id:guid}/reset-password")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
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

    // ═══════════════════════════════════════════════════════════════════════
    // 2. Nhật ký kiểm toán (Audit Trail)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Truy vấn danh sách Nhật ký kiểm toán (Audit Trail) có phân trang & bộ lọc</summary>
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(AuditLogListResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs([FromQuery] AuditLogQueryDto queryDto, CancellationToken ct)
    {
        var result = await _auditLogService.GetAuditLogsAsync(queryDto, ct);
        return Ok(result);
    }

    /// <summary>Xem chi tiết 1 bản ghi Nhật ký kiểm toán kèm giá trị cũ/mới (OldValues/NewValues)</summary>
    [HttpGet("audit-logs/{id:guid}")]
    [ProducesResponseType(typeof(AuditLogDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditLogById(Guid id, CancellationToken ct)
    {
        var result = await _auditLogService.GetAuditLogByIdAsync(id, ct);
        if (result is null)
            return NotFound(new { message = $"Không tìm thấy bản ghi Audit Log với ID {id}" });

        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3. Báo cáo thống kê toàn sàn (Super Admin Analytics)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Thống kê tổng quan các chỉ số hoạt động toàn sàn</summary>
    [HttpGet("dashboard/overview")]
    [ProducesResponseType(typeof(PlatformOverviewStatDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardOverview(CancellationToken ct)
    {
        var result = await _dashboardService.GetOverviewStatsAsync(ct);
        return Ok(result);
    }

    /// <summary>Thống kê mức độ hấp thụ căn hộ NOXH theo dự án</summary>
    [HttpGet("dashboard/absorption")]
    [ProducesResponseType(typeof(List<PlatformAbsorptionStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardAbsorption(CancellationToken ct)
    {
        var result = await _dashboardService.GetAbsorptionStatsAsync(ct);
        return Ok(result);
    }

    /// <summary>Thống kê giải ngân & thu tiền thanh toán + nợ quá hạn + lãi phạt</summary>
    [HttpGet("dashboard/disbursement")]
    [ProducesResponseType(typeof(PlatformDisbursementStatDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardDisbursement(CancellationToken ct)
    {
        var result = await _dashboardService.GetDisbursementStatsAsync(ct);
        return Ok(result);
    }

    /// <summary>Thống kê tỷ lệ hồ sơ hợp lệ / không hợp lệ / vi phạm phục vụ báo cáo nhà nước</summary>
    [HttpGet("dashboard/applications-ratio")]
    [ProducesResponseType(typeof(PlatformApplicationRatioDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardApplicationsRatio(CancellationToken ct)
    {
        var result = await _dashboardService.GetApplicationValidityRatiosAsync(ct);
        return Ok(result);
    }
}

/// <summary>
/// Helper DTO cho API reset password
/// </summary>
public class ResetPasswordRequestDto
{
    public string NewPassword { get; set; } = string.Empty;
}
