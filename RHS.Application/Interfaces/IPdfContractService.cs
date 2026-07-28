using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

/// <summary>
/// Dịch vụ sinh PDF Hợp đồng mua bán nhà ở xã hội
/// (Mẫu số 01 Phụ lục VI – Thông tư 05/2024/TT-BXD).
/// </summary>
public interface IPdfContractService
{
    /// <summary>
    /// Sinh PDF hợp đồng mua bán NOXH và upload lên Cloudinary.
    /// </summary>
    /// <param name="application">Hồ sơ đăng ký (nên Include Apartment, Applicant)</param>
    /// <param name="project">Dự án (nên Include Developer)</param>
    /// <param name="slotCode">Mã suất / tham chiếu hồ sơ</param>
    /// <param name="paymentAmount">Số tiền đặt cọc / đợt đã thanh toán (VND)</param>
    /// <param name="vnpTransactionNo">Mã giao dịch VNPay (nếu có)</param>
    /// <param name="wardManagerName">Tên đại diện bên bán trên HĐ</param>
    /// <returns>URL file PDF trên Cloudinary</returns>
    Task<string> GenerateAndUploadContractAsync(
        HousingApplication application,
        HousingProject project,
        string slotCode,
        decimal paymentAmount,
        string? vnpTransactionNo,
        string wardManagerName);

    /// <summary>
    /// Chỉ sinh PDF bytes (KHÔNG upload). Dùng cho tải on-demand / test.
    /// </summary>
    byte[] GeneratePdfBytesOnly(
        HousingApplication application,
        HousingProject project,
        string slotCode,
        decimal paymentAmount,
        string? vnpTransactionNo,
        string wardManagerName);
}
