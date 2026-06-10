namespace RHS.Application.DTOs.HousingApplications;

/// <summary>
/// Response DTO cho kết quả tạo hồ sơ thành công.
/// Trả về ID và trạng thái ban đầu để client biết hồ sơ đã được tạo.
/// </summary>
public class CreateApplicationResponseDto
{
    public Guid ApplicationId { get; set; }
    public string ApplicationStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO cho kết quả upload tài liệu thành công.
/// </summary>
public class UploadDocumentResponseDto
{
    public Guid DocumentId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO cho kết quả xét duyệt hồ sơ (VO hoặc WM thực hiện).
/// </summary>
public class ReviewResponseDto
{
    public Guid ApplicationId { get; set; }
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime ReviewedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
