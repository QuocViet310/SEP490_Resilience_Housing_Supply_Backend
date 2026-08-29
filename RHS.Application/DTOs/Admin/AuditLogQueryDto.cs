using System;

namespace RHS.Application.DTOs.Admin;

/// <summary>
/// DTO truy vấn danh sách Audit Log dành cho Super Admin
/// </summary>
public class AuditLogQueryDto
{
    public string? EntityName { get; set; }
    public string? Action { get; set; }
    public Guid? UserId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SearchKey { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
