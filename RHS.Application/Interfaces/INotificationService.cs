using RHS.Application.DTOs.Notification;

namespace RHS.Application.Interfaces;

/// <summary>
/// Dịch vụ gửi và quản lý thông báo in-app cho người dùng.
/// Được gọi từ ReviewService, PaymentService, PaymentTimeoutWorker.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Gửi thông báo in-app cho user.
    /// Tạo bản ghi Notification trong DB với IsRead = false.
    /// </summary>
    /// <param name="userId">ID người nhận thông báo</param>
    /// <param name="title">Tiêu đề thông báo</param>
    /// <param name="content">Nội dung chi tiết</param>
    /// <param name="notificationType">Loại thông báo (NotificationTypeConstants)</param>
    Task SendAsync(Guid userId, string title, string content, string notificationType);

    /// <summary>
    /// Lấy danh sách thông báo của user hiện tại, phân trang, mới nhất trước.
    /// </summary>
    Task<PagedNotificationResultDto> GetMyNotificationsAsync(Guid userId, int page, int pageSize);

    /// <summary>Đếm số thông báo chưa đọc</summary>
    Task<int> GetUnreadCountAsync(Guid userId);

    /// <summary>Đánh dấu 1 thông báo đã đọc</summary>
    Task MarkAsReadAsync(Guid notificationId, Guid userId);

    /// <summary>Đánh dấu tất cả thông báo đã đọc</summary>
    Task MarkAllAsReadAsync(Guid userId);
}
