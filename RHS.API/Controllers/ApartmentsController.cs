using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.Apartment;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using System.Security.Claims;

namespace RHS.API.Controllers;

[ApiController]
[Route("api/housing-projects/{projectId:guid}/apartments")]
public class ApartmentsController : ControllerBase
{
    private readonly IApartmentService _apartmentService;
    private readonly ILogger<ApartmentsController> _logger;

    public ApartmentsController(
        IApartmentService apartmentService,
        ILogger<ApartmentsController> logger)
    {
        _apartmentService = apartmentService;
        _logger           = logger;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return Guid.TryParse(claim?.Value, out var id) ? id : Guid.Empty;
    }

    /// <summary>
    /// Lấy danh sách căn hộ của dự án với bộ lọc đa tiêu chí (Tầng, Block, Loại căn, Phân nhóm Ưu tiên/Tiêu chuẩn, Giá, Diện tích).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<ApartmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApartments(
        Guid projectId,
        [FromQuery] ApartmentFilterRequestDto filter,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _apartmentService.GetApartmentsAsync(projectId, filter, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy thông tin chi tiết một căn hộ theo ID.
    /// </summary>
    [HttpGet("{apartmentId:guid}")]
    [ProducesResponseType(typeof(ApartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApartmentById(
        Guid projectId,
        Guid apartmentId,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _apartmentService.GetApartmentByIdAsync(projectId, apartmentId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// [HousingDeveloper / Admin] Tạo mới một căn hộ trong dự án.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.SystemAdministrator},{RoleConstants.DepartmentOfConstruction}")]
    [ProducesResponseType(typeof(ApartmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateApartment(
        Guid projectId,
        [FromBody] CreateApartmentDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        try
        {
            var created = await _apartmentService.CreateApartmentAsync(projectId, userId, dto, ct);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// [HousingDeveloper / Admin] Tạo hàng loạt căn hộ theo sơ đồ tầng/block (Batch create).
    /// </summary>
    [HttpPost("batch")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.SystemAdministrator},{RoleConstants.DepartmentOfConstruction}")]
    [ProducesResponseType(typeof(List<ApartmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> BatchCreateApartments(
        Guid projectId,
        [FromBody] BatchCreateApartmentsRequestDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        try
        {
            var createdList = await _apartmentService.BatchCreateApartmentsAsync(projectId, userId, dto, ct);
            return StatusCode(StatusCodes.Status201Created, new
            {
                message = $"Đã tạo thành công {createdList.Count} căn hộ.",
                data = createdList
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// [HousingDeveloper / Admin] Tải file Excel mẫu (.xlsx) chuẩn để nhập danh sách căn hộ.
    /// </summary>
    [HttpGet("excel-template")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.SystemAdministrator},{RoleConstants.DepartmentOfConstruction}")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadExcelTemplate(
        Guid projectId,
        CancellationToken ct = default)
    {
        try
        {
            var fileBytes = await _apartmentService.GenerateApartmentExcelTemplateAsync(projectId, ct);
            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Template_DanhSachCanHo_DuAn_{projectId:N}.xlsx");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo file Excel mẫu cho dự án {ProjectId}", projectId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi tạo file Excel mẫu." });
        }
    }

    /// <summary>
    /// [HousingDeveloper / Admin] Nhập danh sách căn hộ tự động từ file Excel (.xlsx).
    /// </summary>
    [HttpPost("import-excel")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.SystemAdministrator},{RoleConstants.DepartmentOfConstruction}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApartmentExcelImportResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApartmentExcelImportResultDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ImportApartmentsFromExcel(
        Guid projectId,
        IFormFile file,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        try
        {
            var result = await _apartmentService.ImportApartmentsFromExcelAsync(projectId, userId, file, ct);

            if (result.FailedCount > 0)
            {
                return BadRequest(result);
            }

            return StatusCode(StatusCodes.Status201Created, result);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi import file Excel danh sách căn hộ cho dự án {ProjectId}", projectId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }

    /// <summary>
    /// [HousingDeveloper / Admin] Cập nhật thông tin căn hộ (chỉ khi căn hộ chưa bị ASSIGNED).
    /// </summary>
    [HttpPut("{apartmentId:guid}")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.SystemAdministrator},{RoleConstants.DepartmentOfConstruction}")]
    [ProducesResponseType(typeof(ApartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateApartment(
        Guid projectId,
        Guid apartmentId,
        [FromBody] UpdateApartmentDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        try
        {
            var updated = await _apartmentService.UpdateApartmentAsync(projectId, apartmentId, userId, dto, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// [HousingDeveloper / Admin] Xóa căn hộ khỏi dự án (chỉ khi căn hộ trống - AVAILABLE).
    /// </summary>
    [HttpDelete("{apartmentId:guid}")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.SystemAdministrator},{RoleConstants.DepartmentOfConstruction}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteApartment(
        Guid projectId,
        Guid apartmentId,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        try
        {
            var deleted = await _apartmentService.DeleteApartmentAsync(projectId, apartmentId, userId, ct);
            if (!deleted)
                return NotFound(new { message = "Không tìm thấy căn hộ để xóa." });

            return Ok(new { message = "Xóa căn hộ thành công." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

/// <summary>
/// API xem Sơ đồ mặt bằng (Floor Plan) và Thống kê quỹ căn của dự án.
/// Prefix: /api/housing-projects/{projectId}
/// </summary>
[ApiController]
[Route("api/housing-projects/{projectId:guid}")]
public class ProjectFloorPlanController : ControllerBase
{
    private readonly IApartmentService _apartmentService;

    public ProjectFloorPlanController(IApartmentService apartmentService)
    {
        _apartmentService = apartmentService;
    }

    /// <summary>
    /// Lấy sơ đồ mặt bằng dự án gom nhóm theo Block và Tầng (Floor Plan).
    /// </summary>
    [HttpGet("floor-plan")]
    [ProducesResponseType(typeof(FloorPlanResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFloorPlan(Guid projectId, CancellationToken ct = default)
    {
        try
        {
            var result = await _apartmentService.GetFloorPlanAsync(projectId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy thống kê cơ cấu quỹ căn hộ của dự án (Ưu tiên, Tiêu chuẩn, Trống, Đã cấp, Theo loại phòng, Theo tầng).
    /// </summary>
    [HttpGet("apartment-statistics")]
    [ProducesResponseType(typeof(ApartmentStatisticsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApartmentStatistics(Guid projectId, CancellationToken ct = default)
    {
        try
        {
            var result = await _apartmentService.GetApartmentStatisticsAsync(projectId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
