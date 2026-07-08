using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.HousingApplications;

/// <summary>
/// Request DTO cho Housing Developer (CĐT) thực hiện xét duyệt hồ sơ.
/// </summary>
public class HousingDeveloperReviewRequestDto
{
    /// <summary>
    /// Hành động thực hiện. Giá trị hợp lệ:
    /// "PROPOSE", "REJECT", hoặc "REQUEST_MORE_DOCUMENTS".
    /// </summary>
    [Required(ErrorMessage = "Hành động là bắt buộc.")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Ghi chú/Lý do. BẮT BUỘC khi Action = "REJECT" hoặc "REQUEST_MORE_DOCUMENTS".
    /// </summary>
    [MaxLength(1000, ErrorMessage = "Ghi chú không được quá 1000 ký tự.")]
    public string? Note { get; set; }
}
