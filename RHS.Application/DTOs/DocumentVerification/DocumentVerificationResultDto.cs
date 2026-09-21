using System;

namespace RHS.Application.DTOs.DocumentVerification;

public class DocumentVerificationResultDto
{
    public Guid VerificationId { get; set; }
    public Guid DocumentId { get; set; }
    public string ValidationResult { get; set; } = string.Empty;  // MATCH, MISMATCH, ERROR
    public string? ErrorDetails { get; set; } // Lý do cụ thể để hiển thị cho User sửa
    
    // Thông tin trích xuất được
    public string? ExtractedFullName { get; set; }
    public string? ExtractedCitizenId { get; set; }
    public string? ExtractedAddress { get; set; }
    public string? ExtractedDateOfBirth { get; set; }
    
    // Chi tiết kiểm tra AI (Đúng Tên & Đúng Loại Giấy Tờ)
    public bool IsNameMatch { get; set; }
    public bool IsDocumentTypeMatch { get; set; }
    public string? NameCheckDetails { get; set; }
    public string? DocumentTypeCheckDetails { get; set; }
    
    public DateTime VerifiedAt { get; set; }
}
