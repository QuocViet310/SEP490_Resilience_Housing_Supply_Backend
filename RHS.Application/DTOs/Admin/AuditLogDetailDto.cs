using System;

namespace RHS.Application.DTOs.Admin;

/// <summary>
/// DTO chi tiết bản ghi Audit Log chứa giá trị cũ/mới (OldValues/NewValues)
/// </summary>
public class AuditLogDetailDto
{
    public Guid AuditId { get; set; }
    public Guid? UserId { get; set; }
    public string? UserFullName { get; set; }
    public string? UserEmail { get; set; }
    public string? UserRole { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public DateTime ActionTime { get; set; }
}
