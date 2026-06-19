using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.HousingApplications;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Infrastructure.Exceptions;
using System.Security.Claims;

namespace RHS.API.Controllers;

/// <summary>
/// API quản lý hồ sơ đăng ký nhà ở xã hội.
///
/// Phân quyền theo endpoint:
///   - Applicant : tạo hồ sơ, xem hồ sơ của mình, nộp hồ sơ
///   - Verification Officer : xem tất cả, nhận hồ sơ, xét duyệt
///   - Ward Manager  : xem tất cả, phê duyệt / từ chối / yêu cầu bổ sung
/// </summary>
[ApiController]
[Route("api/housing-applications")]
[Authorize]
public class HousingApplicationsController : ControllerBase
{
    private readonly IHousingApplicationService _applicationService;
    private readonly IReviewService _reviewService;
    private readonly ILogger<HousingApplicationsController> _logger;

    public HousingApplicationsController(
        IHousingApplicationService applicationService,
        IReviewService reviewService,
        ILogger<HousingApplicationsController> logger)
    {
        _applicationService = applicationService;
        _reviewService      = reviewService;
        _logger             = logger;
    }

    // ──────────────────────────────────────────────────────────────
    // APPLICANT: Tạo hồ sơ
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [Applicant] Tạo hồ sơ đăng ký nhà ở xã hội.
    /// Hồ sơ được tạo với trạng thái DRAFT. Cần upload giấy tờ rồi mới nộp.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = RoleConstants.Applicant)]
    [ProducesResponseType(typeof(CreateApplicationResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateApplication(
        [FromBody] CreateApplicationRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var applicantId = GetCurrentUserId();
        if (applicantId == Guid.Empty)
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

        try
        {
            var result = await _applicationService.CreateApplicationAsync(applicantId, request);
            return CreatedAtAction(
                nameof(GetApplicationById),
                new { id = result.ApplicationId },
                result);
        }
        catch (DuplicateApplicationException ex)
        {
            _logger.LogWarning("Duplicate application: {Message}", ex.Message);
            return Conflict(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error creating application: {Message}", ex.Message);
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating housing application.");
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi tạo hồ sơ." });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // APPLICANT: Xem hồ sơ của mình
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [Applicant] Lấy danh sách hồ sơ của chính mình (có phân trang + lọc).
    /// </summary>
    [HttpGet("my")]
    [Authorize(Roles = RoleConstants.Applicant)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyApplications([FromQuery] ApplicationFilterRequestDto filter)
    {
        var applicantId = GetCurrentUserId();
        if (applicantId == Guid.Empty)
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

        try
        {
            var result = await _applicationService.GetMyApplicationsAsync(applicantId, filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving applications for applicant {Id}.", applicantId);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách hồ sơ." });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // OFFICER / MANAGER: Xem tất cả hồ sơ
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [VerificationOfficer | WardManager] Lấy tất cả hồ sơ (có phân trang + lọc).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{RoleConstants.VerificationOfficer},{RoleConstants.WardManager}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllApplications([FromQuery] ApplicationFilterRequestDto filter)
    {
        try
        {
            var result = await _applicationService.GetAllApplicationsAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all housing applications.");
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy danh sách hồ sơ." });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // ALL STAFF + APPLICANT: Xem chi tiết 1 hồ sơ
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [Applicant | VerificationOfficer | WardManager] Lấy chi tiết hồ sơ theo ID.
    /// Bao gồm danh sách tài liệu và lịch sử xét duyệt.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{RoleConstants.Applicant},{RoleConstants.VerificationOfficer},{RoleConstants.WardManager}")]
    [ProducesResponseType(typeof(ApplicationDetailResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetApplicationById(Guid id)
    {
        try
        {
            var result = await _applicationService.GetApplicationByIdAsync(id);
            return Ok(result);
        }
        catch (ApplicationNotFoundException ex)
        {
            _logger.LogWarning("Application not found: {Id}", id);
            return NotFound(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving application {Id}.", id);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy chi tiết hồ sơ." });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // APPLICANT: Nộp hồ sơ (DRAFT → SUBMITTED)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [Applicant] Nộp hồ sơ chính thức.
    /// Yêu cầu: hồ sơ phải ở trạng thái DRAFT, có ít nhất 1 tài liệu,
    /// và CCCD chưa tồn tại trong hồ sơ khác của cùng dự án.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = RoleConstants.Applicant)]
    [ProducesResponseType(typeof(ReviewResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SubmitApplication(Guid id)
    {
        var applicantId = GetCurrentUserId();
        if (applicantId == Guid.Empty)
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

        try
        {
            var result = await _reviewService.SubmitApplicationAsync(id, applicantId);
            return Ok(result);
        }
        catch (ApplicationNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (DuplicateCitizenIdInProjectException ex)
        {
            _logger.LogWarning(
                "Submit blocked for application {Id}: CitizenId '{CitizenId}' already exists in project.",
                id, ex.CitizenId);
            return Conflict(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (ApplicationNotReadyToSubmitException ex)
        {
            _logger.LogWarning("Application {Id} not ready to submit: {Reason}", id, ex.Reason);
            return UnprocessableEntity(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (InvalidApplicationStatusTransitionException ex)
        {
            _logger.LogWarning("Invalid status transition for {Id}: {Message}", id, ex.Message);
            return UnprocessableEntity(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting application {Id}.", id);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi nộp hồ sơ." });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // VERIFICATION OFFICER: Nhận hồ sơ (SUBMITTED → UNDER_REVIEW)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [VerificationOfficer] Nhận hồ sơ để thẩm định.
    /// Chuyển trạng thái SUBMITTED (hoặc NEED_MORE_DOCUMENTS) → UNDER_REVIEW.
    /// </summary>
    [HttpPost("{id:guid}/assign")]
    [Authorize(Roles = RoleConstants.VerificationOfficer)]
    [ProducesResponseType(typeof(ReviewResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AssignApplication(Guid id)
    {
        var officerId = GetCurrentUserId();
        if (officerId == Guid.Empty)
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

        try
        {
            var result = await _reviewService.AssignOfficerAsync(id, officerId);
            return Ok(result);
        }
        catch (ApplicationNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (InvalidApplicationStatusTransitionException ex)
        {
            _logger.LogWarning("VO assign failed for {Id}: {Message}", id, ex.Message);
            return UnprocessableEntity(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning application {Id}.", id);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi nhận hồ sơ." });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // VERIFICATION OFFICER: Xét duyệt (UNDER_REVIEW → APPROVED/REJECTED)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [VerificationOfficer] Xét duyệt hồ sơ.
    /// Action: APPROVE → APPROVED | REJECT (+ Note) → REJECTED.
    /// </summary>
    [HttpPost("{id:guid}/vo-review")]
    [Authorize(Roles = RoleConstants.VerificationOfficer)]
    [ProducesResponseType(typeof(ReviewResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> VerificationOfficerReview(
        Guid id,
        [FromBody] VerificationOfficerReviewRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var officerId = GetCurrentUserId();
        if (officerId == Guid.Empty)
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

        try
        {
            var result = await _reviewService.VerificationOfficerReviewAsync(id, officerId, request);
            return Ok(result);
        }
        catch (ApplicationNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (InvalidApplicationStatusTransitionException ex)
        {
            _logger.LogWarning("VO review failed for {Id}: {Message}", id, ex.Message);
            return UnprocessableEntity(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in VO review for application {Id}.", id);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi xét duyệt hồ sơ." });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // WARD MANAGER: Xét duyệt (UNDER_REVIEW → APPROVED/REJECTED/NEED_MORE_DOCUMENTS)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [WardManager] Xét duyệt hồ sơ.
    /// Action: APPROVE → APPROVED | REJECT (+ Note) → REJECTED
    ///       | REQUEST_MORE_DOCUMENTS (+ Note) → NEED_MORE_DOCUMENTS.
    /// </summary>
    [HttpPost("{id:guid}/wm-review")]
    [Authorize(Roles = RoleConstants.WardManager)]
    [ProducesResponseType(typeof(ReviewResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> WardManagerReview(
        Guid id,
        [FromBody] WardManagerReviewRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var managerId = GetCurrentUserId();
        if (managerId == Guid.Empty)
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

        try
        {
            var result = await _reviewService.WardManagerReviewAsync(id, managerId, request);
            return Ok(result);
        }
        catch (ApplicationNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (InvalidApplicationStatusTransitionException ex)
        {
            _logger.LogWarning("WM review failed for {Id}: {Message}", id, ex.Message);
            return UnprocessableEntity(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WM review for application {Id}.", id);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi xét duyệt hồ sơ." });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Private helper
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy ID người dùng hiện tại từ JWT claim.
    /// Sử dụng pattern nhất quán với các controller khác trong project.
    /// </summary>
    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return Guid.TryParse(claim?.Value, out var id) ? id : Guid.Empty;
    }
}
