using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace RHS.Application.DTOs.CitizenProfile;

/// <summary>
/// Request DTO để upload tài liệu vào Kho tài liệu cá nhân tái sử dụng.
/// </summary>
public class UploadUserDocumentRequestDto
{
    /// <summary>
    /// Loại tài liệu. Sử dụng DocumentTypeConstants.AllowedProfileDocumentTypes.
    /// Ví dụ: CITIZEN_ID_FRONT, CITIZEN_ID_BACK, MARRIAGE_CERTIFICATE, SINGLE_STATUS_CERTIFICATE,
    /// DIVORCE_CERTIFICATE, INCOME_CERTIFICATE, HOUSING_CONDITION_PROOF, MERIT_PERSON_CERTIFICATE, DEPENDENT_PROOF
    /// </summary>
    [Required(ErrorMessage = "Loại tài liệu là bắt buộc.")]
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>File tài liệu đính kèm (PDF hoặc hình ảnh JPG/PNG, tối đa 10MB)</summary>
    [Required(ErrorMessage = "File tài liệu là bắt buộc.")]
    public IFormFile File { get; set; } = null!;

    [MaxLength(500, ErrorMessage = "Mô tả không được quá 500 ký tự.")]
    public string? Description { get; set; }
}

/// <summary>
/// Response DTO trả về thông tin tài liệu trong Kho hồ sơ tái sử dụng.
/// </summary>
public class UserDocumentResponseDto
{
    public Guid DocumentId { get; set; }
    public Guid UserId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentTypeLabel { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Description { get; set; }
    public string VerificationStatus { get; set; } = "PENDING";
    public DateTime UploadedAt { get; set; }
}
