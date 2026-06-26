using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.Interfaces;
using System.Security.Claims;

namespace RHS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>
    /// Lấy danh sách thông báo của người dùng hiện tại (phân trang).
    /// </summary>
    /// <param name="page">Trang hiện tại (mặc định: 1)</param>
    /// <param name="pageSize">Số thông báo mỗi trang (mặc định: 20, tối đa: 100)</param>
    [HttpGet("my")]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _notificationService.GetMyNotificationsAsync(userId.Value, page, pageSize);

        return Ok(new
        {
            success = true,
            data    = result
        });
    }

    /// <summary>
    /// Đếm số thông báo chưa đọc của người dùng hiện tại.
    /// Dùng cho badge notification trên FE.
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var count = await _notificationService.GetUnreadCountAsync(userId.Value);

        return Ok(new
        {
            success     = true,
            unreadCount = count
        });
    }

    /// <summary>
    /// Đánh dấu một thông báo cụ thể là đã đọc.
    /// Chỉ chủ sở hữu thông báo mới có thể đánh dấu.
    /// </summary>
    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        await _notificationService.MarkAsReadAsync(id, userId.Value);

        return Ok(new
        {
            success = true,
            message = "Đã đánh dấu đã đọc"
        });
    }

    /// <summary>
    /// Đánh dấu tất cả thông báo của người dùng hiện tại là đã đọc.
    /// </summary>
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        await _notificationService.MarkAllAsReadAsync(userId.Value);

        return Ok(new
        {
            success = true,
            message = "Đã đánh dấu tất cả đã đọc"
        });
    }

    // ── Helper ──────────────────────────────────────────────────────────
    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (claim != null && Guid.TryParse(claim.Value, out var userId))
            return userId;
        return null;
    }
}
