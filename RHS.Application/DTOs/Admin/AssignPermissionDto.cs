using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.Admin;

/// <summary>
/// DTO để Admin phân quyền cho cán bộ
/// </summary>
public class AssignPermissionDto
{
    [Required(ErrorMessage = "ID cán bộ là bắt buộc")]
    public Guid StaffId { get; set; }

    [Required(ErrorMessage = "Vai trò là bắt buộc")]
    [RegularExpression(@"^(Ward Manager|Verification Officer)$",
        ErrorMessage = "Vai trò phải là 'Ward Manager' hoặc 'Verification Officer'")]
    public string Role { get; set; } = string.Empty;

    [Required(ErrorMessage = "Trạng thái là bắt buộc")]
    [RegularExpression(@"^(Active|Inactive|Suspended)$",
        ErrorMessage = "Trạng thái phải là 'Active', 'Inactive' hoặc 'Suspended'")]
    public string Status { get; set; } = "Active";

    public string? Reason { get; set; }
}
