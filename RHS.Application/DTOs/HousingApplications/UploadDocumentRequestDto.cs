using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace RHS.Application.DTOs.HousingApplications;

/// <summary>
/// Request DTO để upload tài liệu PDF vào hồ sơ.
/// Applicant chọn 1 trong các loại giấy tờ hợp lệ theo DocumentTypeConstants:
///
/// (A) Bắt buộc tất cả:
///   - HOUSING_CONDITION_PROOF: Giấy xác nhận điều kiện nhà ở
///
/// (B) Chứng minh đối tượng (chọn 1 theo nhóm):
///   - POVERTY_HOUSEHOLD_CERTIFICATE: Giấy chứng nhận hộ nghèo/cận nghèo
///   - MERIT_PERSON_CERTIFICATE: Giấy xác nhận người có công với cách mạng
///   - LOW_INCOME_CERTIFICATE: Giấy xác nhận thu nhập thấp đô thị
///   - EMPLOYMENT_CERTIFICATE: Giấy xác nhận đang làm việc tại DN/HTX/KCN
///   - MILITARY_SERVICE_CERTIFICATE: Giấy xác nhận phục vụ lực lượng vũ trang
///   - CIVIL_SERVANT_CERTIFICATE: Giấy xác nhận cán bộ/công chức/viên chức
///   - PUBLIC_HOUSING_RETURN_CERTIFICATE: Văn bản trả lại nhà ở công vụ
///   - LAND_RECOVERY_DECISION: Quyết định thu hồi đất/giải tỏa nhà ở
///
/// (C) Bổ sung:
///   - INCOME_CERTIFICATE: Giấy xác nhận thu nhập (bắt buộc cho một số nhóm)
/// </summary>
public class UploadDocumentRequestDto
{
    /// <summary>
    /// Loại giấy tờ. Giá trị hợp lệ: xem DocumentTypeConstants.AllowedApplicantDocumentTypes.
    /// </summary>
    [Required(ErrorMessage = "Loại giấy tờ là bắt buộc.")]
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// File PDF cần upload. Chỉ chấp nhận định dạng PDF.
    /// Kích thước tối đa: 10MB.
    /// </summary>
    [Required(ErrorMessage = "File là bắt buộc.")]
    public IFormFile File { get; set; } = null!;
}
