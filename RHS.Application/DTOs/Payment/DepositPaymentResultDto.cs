namespace RHS.Application.DTOs.Payment;

/// <summary>
/// Kết quả trả về cho FE khi tra cứu kết quả thanh toán đặt cọc.
/// Gồm thông tin hợp đồng (PDF) và mã bốc thăm (SlotCode).
/// </summary>
public class DepositPaymentResultDto
{
    /// <summary>Mã đơn hàng nội bộ</summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>ID hồ sơ đăng ký</summary>
    public Guid ApplicationId { get; set; }

    /// <summary>Số tiền đã thanh toán (VND)</summary>
    public decimal Amount { get; set; }

    /// <summary>Mã suất bốc thăm</summary>
    public string SlotCode { get; set; } = string.Empty;

    /// <summary>URL file PDF hợp đồng nguyên tắc trên Cloudinary</summary>
    public string PdfUrl { get; set; } = string.Empty;

    /// <summary>Mã giao dịch VNPay</summary>
    public string? VnpTransactionNo { get; set; }

    /// <summary>Thời điểm thanh toán thành công</summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>Tên dự án</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>Họ tên người đăng ký</summary>
    public string ApplicantName { get; set; } = string.Empty;
}
