using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.HousingApplications;

/// <summary>
/// Request DTO cho Verification Officer thực hiện xét duyệt hồ sơ.
/// VO có thể thực hiện 3 hành động từ trạng thái UNDER_REVIEW:
///   - APPROVE                → APPROVED
///   - REJECT (+ Note)        → REJECTED
///   - REQUEST_MORE_DOCUMENTS (+ Note) → NEED_MORE_DOCUMENTS
/// </summary>
public class VerificationOfficerReviewRequestDto
{
    /// <summary>
    /// Hành động thực hiện. Giá trị hợp lệ:
    /// "APPROVE", "REJECT", hoặc "REQUEST_MORE_DOCUMENTS".
    /// </summary>
    [Required(ErrorMessage = "Hành động là bắt buộc.")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Ghi chú/Lý do. BẮT BUỘC khi Action = "REJECT" hoặc "REQUEST_MORE_DOCUMENTS".
    /// Applicant sẽ thấy nội dung này khi xem lại hồ sơ.
    /// Tối đa 1000 ký tự.
    /// </summary>
    [MaxLength(1000, ErrorMessage = "Ghi chú không được quá 1000 ký tự.")]
    public string? Note { get; set; }
}

