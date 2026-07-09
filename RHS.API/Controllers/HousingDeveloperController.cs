using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.HousingApplications;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using System.Security.Claims;

namespace RHS.API.Controllers;

/// <summary>
/// API dành riêng cho Housing Developer (CĐT).
/// Prefix: /api/housing-developer
/// 
/// Bao gồm:
///   - Gửi danh sách hồ sơ đề nghị phê duyệt lên Sở Xây dựng (Task #7)
///   - Xem danh sách chốt cuối (Final List) của dự án (Task #10)
/// </summary>
[ApiController]
[Route("api/housing-developer")]
[Authorize(Roles = RoleConstants.HousingDeveloper)]
public class HousingDeveloperController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly IHousingApplicationService _applicationService;
    private readonly ILogger<HousingDeveloperController> _logger;

    public HousingDeveloperController(
        IReviewService reviewService,
        IHousingApplicationService applicationService,
        ILogger<HousingDeveloperController> logger)
    {
        _reviewService      = reviewService;
        _applicationService = applicationService;
        _logger             = logger;
    }

    // ──────────────────────────────────────────────────────────────
    // Task #7: CĐT gửi danh sách hồ sơ lên Sở Xây dựng
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [HousingDeveloper] Gửi danh sách hồ sơ đã thẩm định lên Sở Xây dựng.
    /// Batch chuyển trạng thái REVIEWING → PENDING_SXD_REVIEW.
    /// Từ thời điểm này, CĐT không có quyền chỉnh sửa các hồ sơ này nữa.
    /// </summary>
    /// <param name="request">Danh sách ApplicationIds cần gửi.</param>
    /// <returns>Danh sách kết quả chuyển trạng thái cho từng hồ sơ.</returns>
    [HttpPost("submit-to-department")]
    [ProducesResponseType(typeof(List<ReviewResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SubmitToDepartment(
        [FromBody] SubmitToDepartmentRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var developerId = GetCurrentUserId();
        if (developerId == Guid.Empty)
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

        try
        {
            var results = await _reviewService.SubmitToDepartmentAsync(developerId, request);
            return Ok(new
            {
                message = $"Đã gửi thành công {results.Count} hồ sơ lên Sở Xây dựng.",
                data = results
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in SubmitToDepartment.");
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized SubmitToDepartment by {DeveloperId}.", developerId);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SubmitToDepartment by {DeveloperId}.", developerId);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi gửi danh sách lên Sở Xây dựng." });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Task #10: Danh sách chốt cuối (Final List) của dự án
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [HousingDeveloper] Lấy danh sách chốt cuối (Final List) của dự án.
    /// Chỉ trả về các hồ sơ có trạng thái DEPOSIT_PAID (đã hoàn tất đặt cọc).
    /// Dữ liệu này dùng để CĐT xem kết quả và SXD export Excel/PDF công bố.
    /// </summary>
    /// <param name="projectId">ID của dự án cần xem Final List.</param>
    /// <returns>Danh sách hồ sơ DEPOSIT_PAID với đầy đủ thông tin applicant + project.</returns>
    [HttpGet("projects/{projectId:guid}/final-list")]
    [ProducesResponseType(typeof(List<FinalListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFinalList(Guid projectId)
    {
        try
        {
            var result = await _applicationService.GetFinalListByProjectAsync(projectId);
            return Ok(new
            {
                projectId,
                totalCount = result.Count,
                items = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving final list for project {ProjectId}.", projectId);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi tải danh sách chốt cuối." });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Private helper
    // ──────────────────────────────────────────────────────────────

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return Guid.TryParse(claim?.Value, out var id) ? id : Guid.Empty;
    }
}
