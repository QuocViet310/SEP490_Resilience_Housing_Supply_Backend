using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.HousingApplications;
using RHS.Application.DTOs.HousingApplications.Dashboard;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Infrastructure.Exceptions;
using System.Security.Claims;

namespace RHS.API.Controllers;

/// <summary>
/// API quản lý hồ sơ đăng ký nhà ở xã hội.
///
/// Phân quyền theo endpoint:
///   - Applicant              : tạo hồ sơ, xem hồ sơ, nộp hồ sơ, hủy hồ sơ
///   - HousingDeveloper (CĐT) : nhận hồ sơ, thẩm định (reject/request docs), dashboard
///   - DepartmentOfConstruction (SXD) : hậu kiểm, phê duyệt / từ chối, dashboard
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
        catch (ActiveApplicationExistsException ex)
        {
            _logger.LogWarning("Active application exists: {Message}", ex.Message);
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
    // APPLICANT: Cập nhật hồ sơ (DRAFT / NEED_MORE_DOCUMENTS)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [Applicant] Cập nhật thông tin form của hồ sơ.
    /// Chỉ cho phép khi hồ sơ ở trạng thái DRAFT hoặc NEED_MORE_DOCUMENTS.
    /// Không cho phép đổi dự án (ProjectId).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleConstants.Applicant)]
    [ProducesResponseType(typeof(ApplicationDetailResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateApplication(
        Guid id,
        [FromBody] UpdateApplicationRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var applicantId = GetCurrentUserId();
        if (applicantId == Guid.Empty)
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

        try
        {
            var result = await _applicationService.UpdateApplicationAsync(applicantId, id, request);
            return Ok(result);
        }
        catch (ApplicationNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error updating application {Id}: {Message}", id, ex.Message);
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating housing application {Id}.", id);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật hồ sơ." });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // APPLICANT: Kiểm tra hồ sơ đang hoạt động (Active App Check)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [Applicant] Kiểm tra xem người dùng hiện tại có hồ sơ nào đang hoạt động hoặc được duyệt hay chưa.
    /// Trả về true nếu có, false nếu không.
    /// </summary>
    [HttpGet("active-check")]
    [Authorize(Roles = RoleConstants.Applicant)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetActiveCheck()
    {
        var applicantId = GetCurrentUserId();
        if (applicantId == Guid.Empty)
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

        try
        {
            var hasActive = await _applicationService.HasActiveApplicationAsync(applicantId);
            return Ok(new 
            { 
                hasActiveApplication = hasActive,
                message = hasActive 
                    ? "Bạn đang có hồ sơ khác ở trạng thái đã nộp hoặc đã được duyệt. Theo quy định, mỗi người chỉ được đăng ký một hồ sơ hoạt động tại một thời điểm."
                    : "Bạn không có hồ sơ hoạt động nào khác. Có thể tạo hồ sơ nháp mới."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking active application status for applicant {ApplicantId}.", applicantId);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi kiểm tra hồ sơ hoạt động." });
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
    /// <summary>
    /// [HousingDeveloper | DepartmentOfConstruction] Lấy tất cả hồ sơ (có phân trang + lọc).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.DepartmentOfConstruction}")]
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
    // DASHBOARD ENDPOINTS
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [HousingDeveloper] Lấy danh sách hồ sơ cho Dashboard của CĐT.
    /// Chỉ hiển thị hồ sơ thuộc dự án của CĐT đang đăng nhập.
    /// </summary>
    [HttpGet("dashboard/developer")]
    [Authorize(Roles = RoleConstants.HousingDeveloper)]
    [ProducesResponseType(typeof(PagedResult<HousingApplicationDashboardItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetHousingDeveloperDashboard(
        [FromQuery] HousingApplicationDashboardQueryDto query)
    {
        try
        {
            // Inject DeveloperId từ JWT để filter chỉ dự án của CĐT đang đăng nhập
            query.DeveloperId = GetCurrentUserId();
            var result = await _applicationService.GetHousingDeveloperDashboardAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Housing Developer dashboard.");
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi tải dữ liệu dashboard." });
        }
    }

    /// <summary>
    /// [DepartmentOfConstruction] Lấy danh sách hồ sơ cho Dashboard của Sở Xây Dựng.
    /// </summary>
    [HttpGet("dashboard/sxd")]
    [Authorize(Roles = RoleConstants.DepartmentOfConstruction)]
    [ProducesResponseType(typeof(PagedResult<HousingApplicationDashboardItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDepartmentOfConstructionDashboard(
        [FromQuery] HousingApplicationDashboardQueryDto query)
    {
        try
        {
            var result = await _applicationService.GetDepartmentOfConstructionDashboardAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Department Of Construction dashboard.");
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi tải dữ liệu dashboard." });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // ALL STAFF + APPLICANT: Xem chi tiết 1 hồ sơ
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <summary>
    /// [Applicant | HousingDeveloper | DepartmentOfConstruction] Lấy chi tiết hồ sơ theo ID.
    /// Bao gồm danh sách tài liệu và lịch sử xét duyệt.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{RoleConstants.Applicant},{RoleConstants.HousingDeveloper},{RoleConstants.DepartmentOfConstruction}")]
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
    // ──────────────────────────────────────────────────────────────
    // HOUSING DEVELOPER: Nhận hồ sơ (SUBMITTED / NEED_MORE_DOCUMENTS → REVIEWING)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [HousingDeveloper] Nhận hồ sơ để thẩm định.
    /// Chuyển trạng thái SUBMITTED (hoặc NEED_MORE_DOCUMENTS) → REVIEWING.
    /// </summary>
    [HttpPatch("{id:guid}/assign")]
    [Authorize(Roles = RoleConstants.HousingDeveloper)]
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
            _logger.LogWarning("Developer assign failed for {Id}: {Message}", id, ex.Message);
            return UnprocessableEntity(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning application {Id}.", id);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi nhận hồ sơ." });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // ──────────────────────────────────────────────────────────────
    // HOUSING DEVELOPER: Xét duyệt (REVIEWING → REJECTED / NEED_MORE_DOCUMENTS)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [HousingDeveloper] Xét duyệt hồ sơ.
    /// Actions: REJECT, REQUEST_MORE_DOCUMENTS.
    /// (PROPOSE đã được thay thế bằng API batch: POST /api/housing-developer/submit-to-department)
    /// </summary>
    [HttpPatch("{id:guid}/developer-review")]
    [Authorize(Roles = RoleConstants.HousingDeveloper)]
    [ProducesResponseType(typeof(ReviewResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> HousingDeveloperReview(
        Guid id,
        [FromBody] HousingDeveloperReviewRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var developerId = GetCurrentUserId();
        if (developerId == Guid.Empty)
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

        try
        {
            var result = await _reviewService.HousingDeveloperReviewAsync(id, developerId, request);
            return Ok(result);
        }
        catch (ApplicationNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (InvalidApplicationStatusTransitionException ex)
        {
            _logger.LogWarning("Developer review failed for {Id}: {Message}", id, ex.Message);
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
            _logger.LogError(ex, "Error in Developer review for application {Id}.", id);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi xét duyệt hồ sơ." });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // DEPARTMENT OF CONSTRUCTION: Hậu kiểm (PENDING_SXD_REVIEW → APPROVED / REJECTED)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [DepartmentOfConstruction] Hậu kiểm và phê duyệt cuối.
    /// Actions: APPROVE, REJECT.
    /// </summary>
    [HttpPatch("{id:guid}/sxd-review")]
    [Authorize(Roles = RoleConstants.DepartmentOfConstruction)]
    [ProducesResponseType(typeof(ReviewResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DepartmentOfConstructionReview(
        Guid id,
        [FromBody] DepartmentOfConstructionReviewRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var sxdUserId = GetCurrentUserId();
        if (sxdUserId == Guid.Empty)
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

        try
        {
            var result = await _reviewService.DepartmentOfConstructionReviewAsync(id, sxdUserId, request);
            return Ok(result);
        }
        catch (ApplicationNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (InvalidApplicationStatusTransitionException ex)
        {
            _logger.LogWarning("SXD review failed for {Id}: {Message}", id, ex.Message);
            return UnprocessableEntity(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SXD review for application {Id}.", id);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi xét duyệt hồ sơ." });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // APPLICANT: Tự hủy hồ sơ (Task #11)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [Applicant] Tự hủy hồ sơ đang xử lý.
    /// Điều kiện: Hồ sơ không ở trạng thái đóng (DEPOSIT_PAID, REJECTED, CANCELED, EXPIRED).
    /// APPROVED không giữ suất vật lý — hủy không hoàn AvailableUnits.
    /// Giải phóng CCCD cho người dân nộp hồ sơ khác.
    /// </summary>
    [HttpPatch("{id:guid}/cancel")]
    [Authorize(Roles = RoleConstants.Applicant)]
    [ProducesResponseType(typeof(ReviewResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CancelApplication(
        Guid id,
        [FromBody] CancelApplicationRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var applicantId = GetCurrentUserId();
        if (applicantId == Guid.Empty)
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

        try
        {
            var result = await _reviewService.CancelApplicationAsync(id, applicantId, request);
            return Ok(result);
        }
        catch (ApplicationNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidApplicationStatusTransitionException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling application {Id}.", id);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi hủy hồ sơ." });
        }
    }
    // ──────────────────────────────────────────────────────────────
    // DEPARTMENT OF CONSTRUCTION: Gắn cờ vi phạm (gian lận đất đai)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [DepartmentOfConstruction] Gắn cờ vi phạm (gian lận đất đai) cho hồ sơ.
    /// Hồ sơ bị gắn cờ vi phạm sẽ được loại bỏ ngay lập tức khỏi danh sách bốc thăm dự án.
    /// </summary>
    [HttpPost("{id:guid}/flag-violation")]
    [Authorize(Roles = RoleConstants.DepartmentOfConstruction)]
    [ProducesResponseType(typeof(ReviewResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FlagViolation(
        Guid id,
        [FromBody] FlagViolationRequestDto request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "Lý do gắn cờ vi phạm là bắt buộc." });

        var sxdUserId = GetCurrentUserId();
        if (sxdUserId == Guid.Empty)
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

        try
        {
            var result = await _reviewService.FlagViolationAsync(id, sxdUserId, request.Reason);
            return Ok(result);
        }
        catch (ApplicationNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flagging violation for application {Id}.", id);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi gắn cờ vi phạm." });
        }
    }

    /// <summary>
    /// [DepartmentOfConstruction] Gỡ cờ vi phạm cho hồ sơ.
    /// </summary>
    [HttpPost("{id:guid}/unflag-violation")]
    [Authorize(Roles = RoleConstants.DepartmentOfConstruction)]
    [ProducesResponseType(typeof(ReviewResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UnflagViolation(Guid id)
    {
        var sxdUserId = GetCurrentUserId();
        if (sxdUserId == Guid.Empty)
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

        try
        {
            var result = await _reviewService.UnflagViolationAsync(id, sxdUserId);
            return Ok(result);
        }
        catch (ApplicationNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unflagging violation for application {Id}.", id);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi gỡ cờ vi phạm." });
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
