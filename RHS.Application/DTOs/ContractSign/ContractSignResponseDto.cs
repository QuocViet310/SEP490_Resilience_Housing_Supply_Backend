namespace RHS.Application.DTOs.ContractSign;

/// <summary>
/// Kết quả trả về sau khi người dân ký (đồng ý) hợp đồng nguyên tắc.
/// </summary>
public class ContractSignResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime? SignedAt { get; set; }
}
