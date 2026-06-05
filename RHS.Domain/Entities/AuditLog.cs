namespace RHS.Domain.Entities;

public class AuditLog
{
    public Guid AuditId { get; set; }

    public Guid UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string IpAddress { get; set; } = string.Empty;

    public DateTime ActionTime { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
}
