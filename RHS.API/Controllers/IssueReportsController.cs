using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.DTOs.IssueReports;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using System.Security.Claims;

namespace RHS.API.Controllers;

[ApiController]
[Route("api/issue-reports")]
[Authorize]
public class IssueReportsController : ControllerBase
{
    private readonly IIssueReportService _service;
    private readonly ILogger<IssueReportsController> _logger;

    public IssueReportsController(IIssueReportService service, ILogger<IssueReportsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Create a new issue report (Any authenticated user)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(IssueReportDetailResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IssueReportDetailResponseDto>> Create([FromBody] CreateIssueReportRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _service.CreateAsync(userId, request);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating issue report");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get issue report by ID (Admin/Staff only)
    /// </summary>
    [HttpGet("/api/admin/issue-reports/{id:guid}")]
    [Authorize(Roles = $"{RoleConstants.SystemAdministrator},Admin")]
    [ProducesResponseType(typeof(IssueReportDetailResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IssueReportDetailResponseDto>> GetById(Guid id)
    {
        try
        {
            var result = await _service.GetByIdAsync(id);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting issue report by id {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get current logged-in user's issue reports (Any authenticated user)
    /// </summary>
    [HttpGet("my-reports")]
    [ProducesResponseType(typeof(PagedResultDto<IssueReportListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResultDto<IssueReportListItemDto>>> GetMyReports(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _service.GetMyReportsAsync(userId, pageIndex, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting my issue reports");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get all issue reports with search/filters (Admin only)
    /// </summary>
    [HttpGet("/api/admin/issue-reports")]
    [Authorize(Roles = $"{RoleConstants.SystemAdministrator},Admin")]
    [ProducesResponseType(typeof(PagedResultDto<IssueReportListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResultDto<IssueReportListItemDto>>> GetAllReports(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? issueType = null)
    {
        try
        {
            var result = await _service.GetAllReportsAsync(pageIndex, pageSize, search, status, issueType);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all issue reports");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Update status of an issue report (Admin only)
    /// </summary>
    [HttpPut("/api/admin/issue-reports/{id:guid}/status")]
    [Authorize(Roles = $"{RoleConstants.SystemAdministrator},Admin")]
    [ProducesResponseType(typeof(IssueReportDetailResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IssueReportDetailResponseDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateIssueReportStatusRequestDto request)
    {
        try
        {
            var result = await _service.UpdateStatusAsync(id, request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for issue report {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while processing your request" });
        }
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return Guid.TryParse(claim?.Value, out var id) ? id : Guid.Empty;
    }
}
