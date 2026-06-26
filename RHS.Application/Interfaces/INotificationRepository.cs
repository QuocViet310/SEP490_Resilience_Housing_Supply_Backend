using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

/// <summary>
/// Repository thao tác CRUD với bảng Notifications.
/// </summary>
public interface INotificationRepository
{
    /// <summary>Tạo thông báo mới</summary>
    Task CreateAsync(Notification notification);

    /// <summary>
    /// Lấy danh sách thông báo của user, sắp xếp mới nhất trước, có phân trang.
    /// Trả về (Items, TotalCount).
    /// </summary>
    Task<(IEnumerable<Notification> Items, int TotalCount)> GetByUserIdAsync(
        Guid userId, int page, int pageSize);

    /// <summary>Đếm số thông báo chưa đọc của user</summary>
    Task<int> CountUnreadAsync(Guid userId);

    /// <summary>Đánh dấu 1 thông báo đã đọc (chỉ nếu thuộc về user)</summary>
    Task MarkAsReadAsync(Guid notificationId, Guid userId);

    /// <summary>Đánh dấu tất cả thông báo của user là đã đọc</summary>
    Task MarkAllAsReadAsync(Guid userId);
}
