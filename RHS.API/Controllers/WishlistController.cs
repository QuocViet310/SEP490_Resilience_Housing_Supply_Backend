using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.Interfaces;
using System.Security.Claims;

namespace RHS.API.Controllers;

/// <summary>
/// Quản lý danh sách yêu thích (Wishlist) của người dùng đối với các dự án nhà ở.
/// Tất cả endpoints yêu cầu đăng nhập (JWT Bearer).
/// </summary>
[ApiController]
[Route("api/wishlist")]
[Authorize]
public class WishlistController : ControllerBase
{
    private readonly IWishlistService _wishlistService;

    public WishlistController(IWishlistService wishlistService)
    {
        _wishlistService = wishlistService;
    }

    // ── Helper: lấy userId từ JWT claim ─────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return Guid.TryParse(claim?.Value, out var id) ? id : null;
    }

    // ── Endpoints ────────────────────────────────────────────────────────

    /// <summary>
    /// Thêm một dự án nhà ở vào danh sách yêu thích.
    /// </summary>
    /// <remarks>
    /// - Trả về `400` nếu dự án đã có trong wishlist.
    /// - Trả về `404` nếu dự án không tồn tại.
    /// </remarks>
    [HttpPost("{projectId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddToWishlist(Guid projectId)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { success = false, message = "Không xác định được người dùng." });

        try
        {
            await _wishlistService.AddToWishlistAsync(userId.Value, projectId);
            return Ok(new { success = true, message = "Đã thêm dự án vào danh sách yêu thích." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Xóa một dự án nhà ở khỏi danh sách yêu thích.
    /// </summary>
    /// <remarks>
    /// - Trả về `404` nếu dự án không có trong wishlist.
    /// </remarks>
    [HttpDelete("{projectId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFromWishlist(Guid projectId)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { success = false, message = "Không xác định được người dùng." });

        try
        {
            await _wishlistService.RemoveFromWishlistAsync(userId.Value, projectId);
            return Ok(new { success = true, message = "Đã xóa dự án khỏi danh sách yêu thích." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy danh sách yêu thích của người dùng hiện tại (có phân trang, mới nhất trước).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetWishlist(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize  = 10)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { success = false, message = "Không xác định được người dùng." });

        var result = await _wishlistService.GetWishlistAsync(userId.Value, pageIndex, pageSize);
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Kiểm tra nhanh xem một dự án có đang trong danh sách yêu thích hay không.
    /// </summary>
    [HttpGet("{projectId:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CheckWishlistStatus(Guid projectId)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { success = false, message = "Không xác định được người dùng." });

        var isInWishlist = await _wishlistService.IsInWishlistAsync(userId.Value, projectId);
        return Ok(new { success = true, data = new { isInWishlist } });
    }

    /// <summary>
    /// Lấy chi tiết một wishlist item theo projectId.
    /// Dùng để kiểm tra nhanh khi hiển thị danh sách dự án (search/browse).
    /// Trả về `404` nếu user chưa thêm dự án đó vào wishlist.
    /// </summary>
    [HttpGet("by-project/{projectId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByProject(Guid projectId)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { success = false, message = "Không xác định được người dùng." });

        var item = await _wishlistService.GetWishlistItemByProjectAsync(userId.Value, projectId);
        if (item is null)
            return NotFound(new
            {
                success = false,
                message = "Dự án này chưa có trong danh sách yêu thích của bạn."
            });

        return Ok(new { success = true, data = item });
    }
}
