using RHS.Application.DTOs.ContractSign;

namespace RHS.Application.Interfaces;

/// <summary>
/// Dịch vụ xử lý nghiệp vụ ký hợp đồng nguyên tắc (giả lập ký số).
/// Người dân bấm "Đồng ý điều khoản" trên App → hệ thống ghi nhận trạng thái đã ký.
/// </summary>
public interface IContractSignService
{
    /// <summary>
    /// Người dân ký (đồng ý) hợp đồng nguyên tắc.
    /// Validate: chỉ owner, hồ sơ DEPOSIT_PAID hoặc FULLY_PAID, chưa ký.
    /// </summary>
    /// <param name="applicantId">ID người dân (từ JWT)</param>
    /// <param name="applicationId">ID hồ sơ đăng ký</param>
    /// <param name="ipAddress">IP address lúc ký (consent log)</param>
    /// <returns>ContractSignResponseDto với kết quả ký</returns>
    Task<ContractSignResponseDto> SignContractAsync(Guid applicantId, Guid applicationId, string? ipAddress);

    /// <summary>
    /// Lấy trạng thái ký hợp đồng nguyên tắc.
    /// </summary>
    /// <param name="applicationId">ID hồ sơ</param>
    /// <returns>ContractSignStatusDto hoặc null nếu chưa có hợp đồng</returns>
    Task<ContractSignStatusDto?> GetSignStatusAsync(Guid applicationId);
}
