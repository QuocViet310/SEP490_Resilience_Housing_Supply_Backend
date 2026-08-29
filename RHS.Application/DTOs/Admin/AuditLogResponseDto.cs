using System;
using System.Collections.Generic;

namespace RHS.Application.DTOs.Admin;

/// <summary>
/// DTO thông tin bản ghi nhật ký kiểm toán trong danh sách
/// </summary>
public class AuditLogResponseDto
{
    public Guid AuditId { get; set; }
    public Guid? UserId { get; set; }
    public string? UserFullName { get; set; }
    public string? UserEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public DateTime ActionTime { get; set; }
}

/// <summary>
/// DTO danh sách phân trang Audit Log
/// </summary>
public class AuditLogListResponseDto
{
    public List<AuditLogResponseDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / (PageSize > 0 ? PageSize : 1));
}
