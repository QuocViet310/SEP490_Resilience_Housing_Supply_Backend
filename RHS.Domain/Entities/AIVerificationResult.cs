namespace RHS.Domain.Entities;

public class AIVerificationResult
{
    public Guid VerificationId { get; set; }

    public Guid DocumentId { get; set; }

    public string ExtractedText { get; set; } = string.Empty;

    public decimal FaceMatchScore { get; set; }

    public decimal RiskScore { get; set; }

    public string ValidationResult { get; set; } = string.Empty;

    public DateTime VerifiedAt { get; set; }

    // ── NEW: Chi tiết so khớp từng field ─────────────────────
    /// <summary>Tên trích xuất từ PDF</summary>
    public string? ExtractedFullName { get; set; }
    
    /// <summary>Số CCCD trích xuất từ PDF</summary>
    public string? ExtractedCitizenId { get; set; }
    
    /// <summary>Địa chỉ trích xuất từ PDF</summary>
    public string? ExtractedAddress { get; set; }
    
    /// <summary>Ngày sinh trích xuất từ PDF</summary>
    public string? ExtractedDateOfBirth { get; set; }
    
    /// <summary>Chi tiết lỗi hoặc lý do lệch thông tin để báo lại cho User</summary>
    public string? ErrorDetails { get; set; }
    
    /// <summary>Model AI đã sử dụng (ví dụ: "gemini-1.5-flash")</summary>
    public string? AiModelUsed { get; set; }

    // Navigation properties
    public ApplicationDocument Document { get; set; } = null!;
}
