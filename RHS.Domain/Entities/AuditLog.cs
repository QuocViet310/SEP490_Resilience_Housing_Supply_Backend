namespace RHS.Domain.Entities;

/// <summary>
/// Bản ghi audit log tự động — mỗi thao tác INSERT/UPDATE/DELETE trên entity
/// đều được ghi lại bởi AppDbContext.SaveChangesAsync override.
/// </summary>
public class AuditLog
{
    public Guid AuditId { get; set; }

    /// <summary>Nullable — Background Worker / Anonymous sẽ không có UserId.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Loại hành động: INSERT, UPDATE, DELETE.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Tên entity bị ảnh hưởng (VD: HousingApplication, Payment).</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>Primary key của entity bị ảnh hưởng.</summary>
    public Guid EntityId { get; set; }

    /// <summary>Giá trị cũ trước thay đổi (JSON). Null nếu INSERT.</summary>
    public string? OldValues { get; set; }

    /// <summary>Giá trị mới sau thay đổi (JSON). Null nếu DELETE.</summary>
    public string? NewValues { get; set; }

    public string IpAddress { get; set; } = string.Empty;

    public DateTime ActionTime { get; set; }

    // Navigation properties
    public User? User { get; set; }
}
