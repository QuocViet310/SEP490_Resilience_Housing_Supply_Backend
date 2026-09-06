using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.Milestone;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using System.Security.Claims;

namespace RHS.API.Controllers;

/// <summary>
/// API quản lý lịch thanh toán do chủ đầu tư thỏa thuận (số đợt không cố định).
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
    /// [HousingDeveloper / Admin] Thiết lập lịch đóng tiền do chủ đầu tư thỏa thuận (số đợt không cố định).
    /// Yêu cầu Điều 89 Luật Nhà ở 2023: lần đầu ≤ 30%, trước bàn giao ≤ 70%, trước giấy chứng nhận ≤ 95%, giữ ≥ 5% đến sổ hồng.
    /// Tổng % = 100%. Tên từng đợt do chủ đầu tư nhập. Thứ tự liên tục 1..N.
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
