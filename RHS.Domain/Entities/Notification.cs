namespace RHS.Domain.Entities;

public class Notification
{
    public Guid NotificationId { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string NotificationType { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
}
