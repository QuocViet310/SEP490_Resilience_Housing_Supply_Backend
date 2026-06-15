namespace RHS.Domain.Entities;

public class IssueReport
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string IssueType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? ScreenshotUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
}
