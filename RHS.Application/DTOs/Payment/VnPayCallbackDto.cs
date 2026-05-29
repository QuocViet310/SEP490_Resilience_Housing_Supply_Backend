using Microsoft.AspNetCore.Mvc;

namespace RHS.Application.DTOs.Payment;

/// <summary>
/// Các query parameters VNPay gửi về ReturnUrl sau khi người dùng thanh toán.
/// Tên field phải khớp chính xác với tên params VNPay (vnp_*).
/// </summary>
public class VnPayCallbackDto
{
    /// <summary>Phiên bản API VNPay (2.1.0)</summary>
    [FromQuery(Name = "vnp_Version")]
    public string? VnpVersion { get; set; }

    /// <summary>Mã website của merchant tại VNPay</summary>
    [FromQuery(Name = "vnp_TmnCode")]
    public string? VnpTmnCode { get; set; }

    /// <summary>Số tiền thanh toán (đã nhân 100)</summary>
    [FromQuery(Name = "vnp_Amount")]
    public long VnpAmount { get; set; }

    /// <summary>Mã ngân hàng thanh toán</summary>
    [FromQuery(Name = "vnp_BankCode")]
    public string? VnpBankCode { get; set; }

    /// <summary>Mã giao dịch tại ngân hàng</summary>
    [FromQuery(Name = "vnp_BankTranNo")]
    public string? VnpBankTranNo { get; set; }

    /// <summary>Loại thẻ (ATM, QRCODE, ...)</summary>
    [FromQuery(Name = "vnp_CardType")]
    public string? VnpCardType { get; set; }

    /// <summary>Nội dung đơn hàng</summary>
    [FromQuery(Name = "vnp_OrderInfo")]
    public string? VnpOrderInfo { get; set; }

    /// <summary>Ngày giờ thanh toán (yyyyMMddHHmmss)</summary>
    [FromQuery(Name = "vnp_PayDate")]
    public string? VnpPayDate { get; set; }

    /// <summary>Mã phản hồi (00 = thành công)</summary>
    [FromQuery(Name = "vnp_ResponseCode")]
    public string? VnpResponseCode { get; set; }

    /// <summary>Mã merchant tại VNPay</summary>
    [FromQuery(Name = "vnp_TmnCode2")]
    public string? VnpTmnCode2 { get; set; }

    /// <summary>Mã giao dịch tại VNPay</summary>
    [FromQuery(Name = "vnp_TransactionNo")]
    public string? VnpTransactionNo { get; set; }

    /// <summary>Trạng thái giao dịch (00 = thành công)</summary>
    [FromQuery(Name = "vnp_TransactionStatus")]
    public string? VnpTransactionStatus { get; set; }

    /// <summary>Mã đơn hàng nội bộ (OrderId của hệ thống RHS)</summary>
    [FromQuery(Name = "vnp_TxnRef")]
    public string? VnpTxnRef { get; set; }

    /// <summary>Chữ ký bảo mật HMAC-SHA512 – dùng để xác minh tính toàn vẹn</summary>
    [FromQuery(Name = "vnp_SecureHash")]
    public string? VnpSecureHash { get; set; }
}
