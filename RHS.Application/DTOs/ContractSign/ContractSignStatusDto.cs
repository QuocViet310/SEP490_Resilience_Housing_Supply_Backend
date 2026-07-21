namespace RHS.Application.DTOs.ContractSign;

/// <summary>
/// Trạng thái ký hợp đồng nguyên tắc (cho FE hiển thị).
/// </summary>
public class ContractSignStatusDto
{
    public Guid ApplicationId { get; set; }
    public bool IsSigned { get; set; }
    public DateTime? SignedAt { get; set; }
    public string? PdfUrl { get; set; }
    public string ApplicationStatus { get; set; } = string.Empty;
}
