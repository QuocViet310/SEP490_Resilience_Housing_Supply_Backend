namespace RHS.Application.DTOs.Notification;

/// <summary>
/// DTO trả về thông tin một thông báo cho FE.
/// </summary>
public class NotificationDto
{
    public Guid NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Kết quả danh sách thông báo có phân trang.
/// </summary>
public class PagedNotificationResultDto
{
    public IEnumerable<NotificationDto> Items { get; set; } = Enumerable.Empty<NotificationDto>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
