namespace RHS.Application.DTOs.Payment;

/// <summary>
/// Các query parameters VNPay gửi về ReturnUrl sau khi người dùng thanh toán.
/// Tên property giữ nguyên PascalCase; controller đọc trực tiếp từ IQueryCollection
/// bằng key "vnp_*" để tránh phụ thuộc vào Microsoft.AspNetCore.Mvc trong Application layer.
/// </summary>
public class VnPayCallbackDto
{
    /// <summary>Phiên bản API VNPay (2.1.0)</summary>
    public string? VnpVersion { get; set; }

    /// <summary>Mã website của merchant tại VNPay</summary>
    public string? VnpTmnCode { get; set; }

    /// <summary>Số tiền thanh toán (đã nhân 100)</summary>
    public long VnpAmount { get; set; }

    /// <summary>Mã ngân hàng thanh toán</summary>
    public string? VnpBankCode { get; set; }

    /// <summary>Mã giao dịch tại ngân hàng</summary>
    public string? VnpBankTranNo { get; set; }

    /// <summary>Loại thẻ (ATM, QRCODE, ...)</summary>
    public string? VnpCardType { get; set; }

    /// <summary>Nội dung đơn hàng</summary>
    public string? VnpOrderInfo { get; set; }

    /// <summary>Ngày giờ thanh toán (yyyyMMddHHmmss)</summary>
    public string? VnpPayDate { get; set; }

    /// <summary>Mã phản hồi (00 = thành công)</summary>
    public string? VnpResponseCode { get; set; }

    /// <summary>Mã giao dịch tại VNPay</summary>
    public string? VnpTransactionNo { get; set; }

    /// <summary>Trạng thái giao dịch (00 = thành công)</summary>
    public string? VnpTransactionStatus { get; set; }

    /// <summary>Mã đơn hàng nội bộ (OrderId của hệ thống RHS)</summary>
    public string? VnpTxnRef { get; set; }

    /// <summary>Chữ ký bảo mật HMAC-SHA512 – dùng để xác minh tính toàn vẹn</summary>
    public string? VnpSecureHash { get; set; }
}
