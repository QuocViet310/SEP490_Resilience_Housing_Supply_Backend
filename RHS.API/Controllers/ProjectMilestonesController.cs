using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.Milestone;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using System.Security.Claims;

namespace RHS.API.Controllers;

/// <summary>
/// API Quản lý và Thiết lập các Đợt thanh toán (3 đến 6 đợt) cho dự án NOXH.
/// Prefix: /api/housing-projects/{projectId}/milestones
/// </summary>
[ApiController]
[Route("api/housing-projects/{projectId:guid}/milestones")]
public class ProjectMilestonesController : ControllerBase
{
    private readonly IProjectMilestoneService _milestoneService;
    private readonly ILogger<ProjectMilestonesController> _logger;

    public ProjectMilestonesController(
        IProjectMilestoneService milestoneService,
        ILogger<ProjectMilestonesController> logger)
    {
        _milestoneService = milestoneService;
        _logger           = logger;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return Guid.TryParse(claim?.Value, out var id) ? id : Guid.Empty;
    }

    /// <summary>
    /// Lấy danh sách các đợt thanh toán đã cấu hình của dự án kèm tổng hợp tỷ lệ phần trăm (%).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProjectMilestonesResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMilestones(Guid projectId, CancellationToken ct = default)
    {
        try
        {
            var result = await _milestoneService.GetProjectMilestonesAsync(projectId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// [HousingDeveloper / Admin] Thiết lập / Cập nhật trọn gói 3 đến 6 đợt đóng tiền cho dự án.
    /// Yêu cầu:
    ///   - Bắt buộc từ 3 đến 6 đợt đóng tiền.
    ///   - Tổng tỷ lệ % của tất cả các đợt phải đúng bằng 100%.
    ///   - Đợt 1 tối đa 30% theo quy định NOXH.
    ///   - Đợt cuối (Sổ hồng) giữ lại 5%.
    ///   - Thứ tự đợt liên tục từ 1..N.
    ///   - Sự kiện kích hoạt hợp lệ (ON_LOTTERY_WON, ON_CONTRACT_SIGNED, CONSTRUCTION_ROUGH_FLOOR, ROOFING_COMPLETED, HANDOVER, RED_BOOK_ISSUED).
    /// </summary>
    [HttpPut]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.SystemAdministrator},{RoleConstants.DepartmentOfConstruction}")]
    [ProducesResponseType(typeof(ProjectMilestonesResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfigureMilestones(
        Guid projectId,
        [FromBody] ConfigureProjectMilestonesRequestDto request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        try
        {
            var result = await _milestoneService.ConfigureProjectMilestonesAsync(projectId, userId, request, ct);
            return Ok(new
            {
                success = true,
                message = $"Thiết lập thành công {result.TotalMilestones} đợt đóng tiền cho dự án.",
                data = result
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi cấu hình đợt thanh toán cho dự án {ProjectId}", projectId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Đã xảy ra lỗi khi cấu hình đợt thanh toán." });
        }
    }
}
