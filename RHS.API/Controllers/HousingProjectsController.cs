using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using System.Security.Claims;

namespace RHS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HousingProjectsController : ControllerBase
{
    private readonly IHousingProjectService _service;
    private readonly IHousingApplicationService _applicationService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<HousingProjectsController> _logger;

    public HousingProjectsController(
        IHousingProjectService service,
        IHousingApplicationService applicationService,
        IUserRepository userRepository,
        ILogger<HousingProjectsController> logger)
    {
        _service = service;
        _applicationService = applicationService;
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get housing projects with search and filtering support
    /// </summary>
    /// <param name="pageIndex">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (default: 12, max: 100)</param>
    /// <param name="search">Search by project name</param>
    /// <param name="province">Filter by province</param>
    /// <param name="district">Filter by district (legacy)</param>
    /// <param name="ward">Filter by phường/xã (địa giới API v2)</param>
    /// <param name="minPrice">Minimum price</param>
    /// <param name="maxPrice">Maximum price</param>
    /// <param name="minArea">Minimum area</param>
    /// <param name="maxArea">Maximum area</param>
    /// <param name="statusId">Filter by status ID</param>
    /// <param name="statusCode">Filter by status code (e.g. OPEN, UPCOMING, Open_For_Registration)</param>
    /// <returns>Paginated list of housing projects</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResultDto<HousingProjectResponseDto>>> GetHousingProjects(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? search = null,
        [FromQuery] string? province = null,
        [FromQuery] string? district = null,
        [FromQuery] string? ward = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] double? minArea = null,
        [FromQuery] double? maxArea = null,
        [FromQuery] Guid? statusId = null,
        [FromQuery] string? statusCode = null)
    {
        try
        {
            var request = new HousingProjectFilterRequestDto
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                Search = search,
                Province = province,
                District = district,
                Ward = ward,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                MinArea = minArea,
                MaxArea = maxArea,
                StatusId = statusId,
                StatusCode = statusCode
            };

            Guid? currentUserId = null;
            string? currentUserRole = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                currentUserId = userId;
                var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
                currentUserRole = roleClaim?.Value;
            }

            var result = await _service.GetHousingProjectsAsync(request, currentUserId, currentUserRole);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrieving housing projects");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get housing project detail by ID
    /// </summary>
    /// <param name="id">Housing project ID</param>
    /// <returns>Housing project detail</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<HousingProjectResponseDto>> GetHousingProjectById(Guid id)
    {
        try
        {
            var result = await _service.GetHousingProjectByIdAsync(id);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Housing project not found with ID: {ProjectId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrieving housing project detail");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Create a new housing project (CĐT / Admin / SXD).
    /// CĐT: DeveloperId luôn lấy từ JWT. Admin/SXD: có thể truyền DeveloperId trên form.
    /// </summary>
    /// <param name="request">Create housing project request</param>
    /// <returns>Created housing project</returns>
    [HttpPost]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.SystemAdministrator},{RoleConstants.DepartmentOfConstruction}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<HousingProjectResponseDto>> CreateHousingProject(
        [FromForm] CreateHousingProjectRequestDto request)
    {
        try
        {
            var developerId = ResolveDeveloperIdForCreate(request.DeveloperId);
            var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
            if (roleClaim?.Value == RoleConstants.HousingDeveloper && !developerId.HasValue)
            {
                return BadRequest(new { message = "Không xác định được tài khoản CĐT từ token. Vui lòng đăng nhập lại." });
            }

            var result = await _service.CreateHousingProjectAsync(request, developerId);
            return CreatedAtAction(nameof(GetHousingProjectById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error while creating housing project");
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while creating housing project");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while creating housing project");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Update a housing project (Chỉ chỉnh sửa khi trạng thái dự án là PENDING)
    /// </summary>
    /// <param name="id">Housing project ID</param>
    /// <param name="request">Update housing project request</param>
    /// <returns>Updated housing project</returns>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.SystemAdministrator},{RoleConstants.DepartmentOfConstruction}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<HousingProjectResponseDto>> UpdateHousingProject(
        Guid id,
        [FromForm] UpdateHousingProjectRequestDto request)
    {
        try
        {
            Guid? claimDeveloperId = null;
            var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (roleClaim?.Value == RoleConstants.HousingDeveloper
                && userIdClaim != null
                && Guid.TryParse(userIdClaim.Value, out var cdtId)
                && cdtId != Guid.Empty)
            {
                claimDeveloperId = cdtId;
            }

            var result = await _service.UpdateHousingProjectAsync(id, request, claimDeveloperId);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error while updating housing project");
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while updating housing project");
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating housing project");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Delete a housing project - soft delete (Admin/Officer only)
    /// </summary>
    /// <param name="id">Housing project ID</param>
    /// <returns>No content</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteHousingProject(Guid id)
    {
        try
        {
            await _service.DeleteHousingProjectAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Housing project not found with ID: {ProjectId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting housing project");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Approve or Reject a housing project (Department of Construction only)
    /// </summary>
    [HttpPatch("{id}/status")]
    [Authorize(Roles = RoleConstants.DepartmentOfConstruction)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<HousingProjectResponseDto>> UpdateProjectStatus(
        Guid id,
        [FromQuery] string action,
        [FromQuery] string? rejectReason = null)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return BadRequest(new { message = "Action is required." });
        }

        try
        {
            var result = await _service.UpdateProjectStatusAsync(id, action, rejectReason);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error while updating status for housing project {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Project not found or state invalid for {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while updating status for housing project {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Đổi nhanh trạng thái vòng đời dự án (UPCOMING / OPEN / CLOSED / FULL).
    /// Dùng mở/đóng nhận hồ sơ khi demo hoặc vận hành — không thay luồng SXD duyệt PENDING.
    /// </summary>
    [HttpPatch("{id}/lifecycle-status")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<HousingProjectResponseDto>> ChangeLifecycleStatus(
        Guid id,
        [FromBody] ChangeHousingProjectLifecycleStatusRequestDto request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.StatusCode))
        {
            return BadRequest(new { message = "StatusCode là bắt buộc (UPCOMING | OPEN | CLOSED | FULL)." });
        }

        try
        {
            var result = await _service.ChangeLifecycleStatusAsync(id, request.StatusCode, request.Note);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error while changing lifecycle status for project {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Project not found or status missing for {Id}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing lifecycle status for housing project {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Lấy thống kê phân tích danh sách hồ sơ đủ điều kiện so với số căn có sẵn cho CĐT.
    /// </summary>
    [HttpGet("{id}/application-evaluation")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator}")]
    public async Task<ActionResult<ProjectApplicationEvaluationDto>> GetApplicationEvaluation(Guid id)
    {
        try
        {
            var result = await _applicationService.GetProjectApplicationEvaluationAsync(id);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting application evaluation for project {ProjectId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }

    /// <summary>
    /// CĐT thực thi quyết định quy trình cho danh sách hồ sơ đủ điều kiện.
    /// </summary>
    [HttpPost("{id}/developer-decision")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.SystemAdministrator}")]
    public async Task<IActionResult> ExecuteDeveloperDecision(
        Guid id, [FromBody] DeveloperWorkflowDecisionRequestDto request)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");
            var userId = string.IsNullOrEmpty(userIdStr) ? Guid.Empty : Guid.Parse(userIdStr);

            var result = await _applicationService.ExecuteDeveloperDecisionAsync(id, request, userId);
            return Ok(new { success = result, message = "Thực thi quyết định của CĐT thành công." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing developer decision for project {ProjectId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Upload file mô hình 3D (.glb <= 5MB) cho căn hộ lên Cloudinary
    /// </summary>
    [HttpPost("upload-3d-model")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload3DModel(
        IFormFile file,
        [FromServices] IFileStorageService fileStorageService)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn file .glb để upload." });
        }

        if (!fileStorageService.IsValid3DModelFile(file))
        {
            return BadRequest(new { message = "File không hợp lệ. Chỉ chấp nhận file định dạng .glb và kích thước không quá 5MB." });
        }

        try
        {
            var secureUrl = await fileStorageService.Upload3DModelAsync(file);
            return Ok(new { url = secureUrl, message = "Upload file 3D .glb thành công." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi upload file 3D .glb");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }

    /// <summary>
    /// CĐT → luôn gán JWT user id. Admin/SXD → dùng DeveloperId trên form (nếu có).
    /// </summary>
    private Guid? ResolveDeveloperIdForCreate(Guid? requestedDeveloperId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
        var role = roleClaim?.Value;

        if (role == RoleConstants.HousingDeveloper
            && userIdClaim != null
            && Guid.TryParse(userIdClaim.Value, out var cdtId)
            && cdtId != Guid.Empty)
        {
            return cdtId;
        }

        if (requestedDeveloperId.HasValue && requestedDeveloperId.Value != Guid.Empty)
            return requestedDeveloperId;

        return null;
    }
}
