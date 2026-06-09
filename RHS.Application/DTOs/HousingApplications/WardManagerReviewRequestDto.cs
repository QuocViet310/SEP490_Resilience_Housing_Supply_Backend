using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.HousingApplications;

/// <summary>
/// Request DTO cho Ward Manager thực hiện xét duyệt hồ sơ.
/// WM có thể: Approve, Reject, hoặc Request More Documents (từ trạng thái UNDER_REVIEW).
/// </summary>
public class WardManagerReviewRequestDto
{
    /// <summary>
    /// Hành động thực hiện. Giá trị hợp lệ:
    /// "APPROVE", "REJECT", hoặc "REQUEST_MORE_DOCUMENTS".
    /// </summary>
    [Required(ErrorMessage = "Hành động là bắt buộc.")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Ghi chú/Lý do. BẮT BUỘC khi Action = "REJECT" hoặc "REQUEST_MORE_DOCUMENTS".
    /// Tối đa 1000 ký tự.
    /// </summary>
    [MaxLength(1000, ErrorMessage = "Ghi chú không được quá 1000 ký tự.")]
    public string? Note { get; set; }
}
