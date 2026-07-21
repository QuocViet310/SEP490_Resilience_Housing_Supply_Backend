using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.Announcement;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using System.Security.Claims;

namespace RHS.API.Controllers;

[ApiController]
[Route("api/announcements")]
public class AnnouncementController : ControllerBase
{
    private readonly IAnnouncementService _service;
    private readonly ILogger<AnnouncementController> _logger;

    public AnnouncementController(IAnnouncementService service, ILogger<AnnouncementController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC ENDPOINTS (AllowAnonymous)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Danh sách thông báo đã publish (public, phân trang, search, filter type).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedAnnouncementResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublished(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? type = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetPublishedAsync(page, pageSize, type, search, ct);
        return Ok(result);
    }

    /// <summary>
    /// Chi tiết thông báo theo ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        if (result is null)
            return NotFound(new { message = "Không tìm thấy thông báo." });
        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════════
    // MANAGEMENT ENDPOINTS (SXD / Admin)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Danh sách quản lý (tất cả status, bao gồm Draft/Archived) — chỉ SXD/Admin.
    /// </summary>
    [HttpGet("management")]
    [Authorize(Roles = $"{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator}")]
    [ProducesResponseType(typeof(PagedAnnouncementResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetForManagement(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetAllForManagementAsync(page, pageSize, type, status, search, ct);
        return Ok(result);
    }

    /// <summary>
    /// Tạo mới thông báo — chỉ SXD/Admin.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator}")]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAnnouncementRequestDto request, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _service.CreateAsync(userId, request, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating announcement");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Đã xảy ra lỗi khi tạo thông báo." });
        }
    }

    /// <summary>
    /// Cập nhật thông báo — chỉ SXD/Admin.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator}")]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateAnnouncementRequestDto request, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _service.UpdateAsync(id, userId, request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating announcement {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Đã xảy ra lỗi khi cập nhật thông báo." });
        }
    }

    /// <summary>
    /// Xóa mềm thông báo — chỉ SXD/Admin.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _service.DeleteAsync(id, userId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting announcement {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Đã xảy ra lỗi khi xóa thông báo." });
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ATTACHMENT ENDPOINTS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Upload file đính kèm cho announcement — chỉ SXD/Admin.
    /// Hỗ trợ PDF, JPEG, PNG, WebP. Tối đa 10MB/file, 5 file/announcement.
    /// </summary>
    [HttpPost("{id:guid}/attachments")]
    [Authorize(Roles = $"{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator}")]
    [ProducesResponseType(typeof(AnnouncementAttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    public async Task<IActionResult> AddAttachment(
        Guid id, IFormFile file, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _service.AddAttachmentAsync(id, userId, file, ct);
            return CreatedAtAction(nameof(GetById), new { id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding attachment to announcement {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Đã xảy ra lỗi khi upload file đính kèm." });
        }
    }

    /// <summary>
    /// Xóa file đính kèm — chỉ SXD/Admin.
    /// </summary>
    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [Authorize(Roles = $"{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAttachment(
        Guid id, Guid attachmentId, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _service.DeleteAttachmentAsync(id, attachmentId, userId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting attachment {AttachmentId} from announcement {Id}", attachmentId, id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Đã xảy ra lỗi khi xóa file đính kèm." });
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return Guid.TryParse(claim?.Value, out var id) ? id : Guid.Empty;
    }
}
