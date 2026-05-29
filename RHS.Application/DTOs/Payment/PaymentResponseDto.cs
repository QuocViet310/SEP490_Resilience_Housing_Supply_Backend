namespace RHS.Application.DTOs.Payment;

/// <summary>
/// Kết quả trả về sau khi tạo yêu cầu thanh toán VNPay
/// </summary>
public class PaymentResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>URL redirect sang cổng thanh toán VNPay (chỉ có khi Success = true)</summary>
    public string? PaymentUrl { get; set; }

    /// <summary>Mã đơn hàng nội bộ</summary>
    public string? OrderId { get; set; }

    /// <summary>Số tiền (VND)</summary>
    public decimal? Amount { get; set; }
}

/// <summary>
/// Kết quả tra cứu thông tin một giao dịch
/// </summary>
public class PaymentInfoDto
{
    public Guid Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string OrderInfo { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    /// <summary>Pending | Success | Failed | Cancelled</summary>
    public string Status { get; set; } = string.Empty;

    public string? VnpResponseCode { get; set; }
    public string? VnpTransactionNo { get; set; }
    public string? VnpBankCode { get; set; }
    public string? VnpPayDate { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}
