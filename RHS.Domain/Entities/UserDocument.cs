namespace RHS.Domain.Entities;

/// <summary>
/// Tài liệu / Giấy tờ cá nhân được lưu trữ trong Kho tài liệu tái sử dụng (Citizen Document Vault).
/// Cho phép người dân tải lên và lưu sẵn CCCD, Giấy kết hôn/độc thân, Giấy xác nhận thu nhập,
/// Giấy chứng nhận đối tượng ưu tiên, Giấy xác nhận điều kiện nhà ở để tái sử dụng khi nộp hồ sơ NOXH.
/// </summary>
public class UserDocument
{
    public Guid DocumentId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Loại giấy tờ. Sử dụng DocumentTypeConstants.AllowedProfileDocumentTypes.
    /// Ví dụ: CITIZEN_ID_FRONT, CITIZEN_ID_BACK, MARRIAGE_CERTIFICATE, SINGLE_STATUS_CERTIFICATE,
    /// INCOME_CERTIFICATE, HOUSING_CONDITION_PROOF, MERIT_PERSON_CERTIFICATE, DEPENDENT_PROOF...
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>Tên file gốc khi upload</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>URL lưu trữ file trên Cloudinary / Blob Storage</summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>Kích thước file (bytes)</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>Mô tả bổ sung hoặc ghi chú về giấy tờ</summary>
    public string? Description { get; set; }

    /// <summary>Trạng thái xác minh tài liệu: PENDING, VERIFIED, REJECTED</summary>
    public string VerificationStatus { get; set; } = "PENDING";

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Navigation properties
    // ─────────────────────────────────────────────────────────────

    public User User { get; set; } = null!;
}
