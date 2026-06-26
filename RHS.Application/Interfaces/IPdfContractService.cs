using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

/// <summary>
/// Dịch vụ sinh PDF hợp đồng nguyên tắc mua nhà ở xã hội.
/// Tạo file PDF từ template, upload lên Cloudinary, trả về URL.
/// </summary>
public interface IPdfContractService
{
    /// <summary>
    /// Sinh PDF hợp đồng nguyên tắc và upload lên Cloudinary.
    /// </summary>
    /// <param name="application">Hồ sơ đăng ký (bao gồm thông tin người đăng ký)</param>
    /// <param name="project">Dự án nhà ở xã hội</param>
    /// <param name="slotCode">Mã suất bốc thăm đã sinh</param>
    /// <param name="paymentAmount">Số tiền đã thanh toán (VND)</param>
    /// <param name="vnpTransactionNo">Mã giao dịch VNPay</param>
    /// <param name="wardManagerName">Tên cán bộ quản lý địa bàn (Ward Manager)</param>
    /// <returns>URL file PDF trên Cloudinary</returns>
    Task<string> GenerateAndUploadContractAsync(
        HousingApplication application,
        HousingProject project,
        string slotCode,
        decimal paymentAmount,
        string? vnpTransactionNo,
        string wardManagerName);
}
