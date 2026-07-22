using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.DTOs.PublicPostCheck;
using RHS.Application.Interfaces;

namespace RHS.API.Controllers;

/// <summary>
/// API Tra cứu Hậu Kiểm Công Khai (Public Portal)
/// Không yêu cầu đăng nhập (khách vãng lai / toàn dân giám sát).
/// Cung cấp dữ liệu mua trúng nhà ở xã hội và cảnh báo cấm chuyển nhượng 5 năm (Nghị định 100).
/// </summary>
[ApiController]
[Route("api/public/post-check-list")]
[AllowAnonymous]
public class PublicPostCheckController : ControllerBase
{
    private readonly IPublicPostCheckService _service;

    public PublicPostCheckController(IPublicPostCheckService service)
    {
        _service = service;
    }

    /// <summary>
    /// Tra cứu danh sách đối tượng đã giao dịch/mua trúng nhà ở xã hội công khai.
    /// Cho phép phân trang và lọc theo tên, CCCD, dự án, địa bàn (tỉnh/huyện), năm.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<PublicPostCheckListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublicList(
        [FromQuery] PublicPostCheckFilterDto filter,
        CancellationToken ct)
    {
        var result = await _service.GetPublicPostCheckListAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Tra cứu chi tiết một hồ sơ trúng nhà ở xã hội công khai theo ApplicationId.
    /// </summary>
    [HttpGet("{applicationId:guid}")]
    [ProducesResponseType(typeof(PublicPostCheckDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(
        Guid applicationId,
        CancellationToken ct)
    {
        var result = await _service.GetPublicPostCheckDetailAsync(applicationId, ct);
        if (result == null)
        {
            return NotFound(new { message = "Không tìm thấy dữ liệu hậu kiểm công khai cho hồ sơ này." });
        }
        return Ok(result);
    }

    /// <summary>
    /// Tra cứu nhanh theo số CCCD (12 số) để xác minh nghĩa vụ sở hữu & cấm sang nhượng NOXH.
    /// </summary>
    [HttpGet("verify-citizen")]
    [ProducesResponseType(typeof(PublicCitizenVerificationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyCitizen(
        [FromQuery] string citizenId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(citizenId))
        {
            return BadRequest(new { message = "Vui lòng nhập số CCCD để tra cứu." });
        }

        var result = await _service.VerifyCitizenOwnershipAsync(citizenId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Lấy thống kê số liệu công khai (Tổng số căn NOXH phân bổ, theo Tỉnh/Thành, Dự án).
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(PublicPostCheckStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var stats = await _service.GetPublicPostCheckStatsAsync(ct);
        return Ok(stats);
    }
}
