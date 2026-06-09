using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace RHS.Application.DTOs.HousingApplications;

/// <summary>
/// Request DTO để upload tài liệu PDF vào hồ sơ.
/// Applicant CHỈ ĐƯỢC chọn 1 trong 2 loại:
///   - HOUSING_CONDITION_PROOF: Giấy tờ chứng minh điều kiện nhà ở
///   - POVERTY_HOUSEHOLD_CERTIFICATE: Giấy chứng nhận hộ nghèo/cận nghèo
/// </summary>
public class UploadDocumentRequestDto
{
    /// <summary>
    /// Loại giấy tờ. Giá trị hợp lệ: xem DocumentTypeConstants.
    /// "HOUSING_CONDITION_PROOF" hoặc "POVERTY_HOUSEHOLD_CERTIFICATE".
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
