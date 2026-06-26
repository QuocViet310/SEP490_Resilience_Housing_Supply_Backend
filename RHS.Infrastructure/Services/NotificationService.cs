using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.Notification;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Triển khai INotificationService — gửi và quản lý thông báo in-app.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository repository,
        ILogger<NotificationService> logger)
    {
        _repository = repository;
        _logger     = logger;
    }

    /// <inheritdoc/>
    public async Task SendAsync(Guid userId, string title, string content, string notificationType)
    {
        var notification = new Notification
        {
            NotificationId   = Guid.NewGuid(),
            UserId           = userId,
            Title            = title,
            Content          = content,
            NotificationType = notificationType,
            IsRead           = false,
            CreatedAt        = DateTime.UtcNow
        };

        await _repository.CreateAsync(notification);

        _logger.LogInformation(
            "Notification sent: UserId={UserId}, Type={Type}, Title=\"{Title}\".",
            userId, notificationType, title);
    }

    /// <inheritdoc/>
    public async Task<PagedNotificationResultDto> GetMyNotificationsAsync(
        Guid userId, int page, int pageSize)
    {
        // Đảm bảo page và pageSize hợp lệ
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await _repository.GetByUserIdAsync(userId, page, pageSize);

        return new PagedNotificationResultDto
        {
            Items = items.Select(n => new NotificationDto
            {
                NotificationId   = n.NotificationId,
                Title            = n.Title,
                Content          = n.Content,
                NotificationType = n.NotificationType,
                IsRead           = n.IsRead,
                CreatedAt        = n.CreatedAt
            }),
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize
        };
    }

    /// <inheritdoc/>
    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        return await _repository.CountUnreadAsync(userId);
    }

    /// <inheritdoc/>
    public async Task MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        await _repository.MarkAsReadAsync(notificationId, userId);

        _logger.LogDebug(
            "Notification {NotifId} marked as read by User {UserId}.",
            notificationId, userId);
    }

    /// <inheritdoc/>
    public async Task MarkAllAsReadAsync(Guid userId)
    {
        await _repository.MarkAllAsReadAsync(userId);

        _logger.LogInformation(
            "All notifications marked as read for User {UserId}.", userId);
    }
}
