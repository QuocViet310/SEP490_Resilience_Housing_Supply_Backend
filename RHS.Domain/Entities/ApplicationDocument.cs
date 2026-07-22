namespace RHS.Domain.Entities;

/// <summary>
/// Tài liệu đính kèm trong hồ sơ đăng ký nhà ở xã hội.
/// Chỉ chấp nhận file PDF.
/// 
/// Hồ sơ cần nộp đủ các loại giấy tờ tùy theo nhóm đối tượng:
///   (A) HOUSING_CONDITION_PROOF — bắt buộc tất cả
///   (B) 1 giấy tờ chứng minh đối tượng (tùy nhóm đối tượng):
///       POVERTY_HOUSEHOLD_CERTIFICATE, MERIT_PERSON_CERTIFICATE,
///       LOW_INCOME_CERTIFICATE, EMPLOYMENT_CERTIFICATE,
///       MILITARY_SERVICE_CERTIFICATE, CIVIL_SERVANT_CERTIFICATE,
///       PUBLIC_HOUSING_RETURN_CERTIFICATE, LAND_RECOVERY_DECISION
///   (C) INCOME_CERTIFICATE — bắt buộc cho một số nhóm
/// Xem chi tiết: DocumentTypeConstants.
/// </summary>
public class ApplicationDocument
{
    public Guid DocumentId { get; set; }

    public Guid ApplicationId { get; set; }

    /// <summary>
    /// Người upload tài liệu (FK → Users.Id).
    /// Thường là ApplicantId.
    /// </summary>
    public Guid UploadedBy { get; set; }

    /// <summary>
    /// Loại giấy tờ. Sử dụng DocumentTypeConstants.
    /// Xem danh sách: DocumentTypeConstants.AllowedApplicantDocumentTypes.
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>Tên file gốc khi upload</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>URL lưu trữ file (Cloudinary hoặc Azure Blob)</summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>Kích thước file (bytes)</summary>
    public long FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; }

    /// <summary>Trạng thái xác minh tài liệu: PENDING, VERIFIED, REJECTED</summary>
    public string VerificationStatus { get; set; } = "PENDING";

    // ─────────────────────────────────────────────────────────────
    // Navigation properties
    // ─────────────────────────────────────────────────────────────

    public HousingApplication HousingApplication { get; set; } = null!;

    /// <summary>Người upload tài liệu (navigation)</summary>
    public User UploadedByUser { get; set; } = null!;

    public AIVerificationResult? VerificationResult { get; set; }
}
