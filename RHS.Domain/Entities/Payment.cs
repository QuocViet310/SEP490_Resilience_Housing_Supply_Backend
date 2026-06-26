namespace RHS.Domain.Entities;

public class Payment
{
    public Guid Id { get; set; }

    /// <summary>Người dùng thực hiện thanh toán</summary>
    public Guid UserId { get; set; }

    /// <summary>Dự án nhà ở liên kết (nếu có)</summary>
    public Guid? HousingProjectId { get; set; }

    /// <summary>Hồ sơ đăng ký liên kết (dùng cho thanh toán đặt cọc)</summary>
    public Guid? ApplicationId { get; set; }

    /// <summary>Mã đơn hàng nội bộ (duy nhất, dùng cho vnp_TxnRef)</summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>Mô tả đơn hàng</summary>
    public string OrderInfo { get; set; } = string.Empty;

    /// <summary>Số tiền (VND)</summary>
    public decimal Amount { get; set; }

    /// <summary>Trạng thái: Pending | Success | Failed | Cancelled</summary>
    public string Status { get; set; } = "Pending";

    // ── Thông tin phản hồi từ VNPay ──────────────────────────────────────
    /// <summary>Mã phản hồi VNPay (00 = thành công)</summary>
    public string? VnpResponseCode { get; set; }

    /// <summary>Mã giao dịch bên VNPay</summary>
    public string? VnpTransactionNo { get; set; }

    /// <summary>Mã ngân hàng</summary>
    public string? VnpBankCode { get; set; }

    /// <summary>Mã giao dịch ngân hàng</summary>
    public string? VnpBankTranNo { get; set; }

    /// <summary>Loại thẻ thanh toán</summary>
    public string? VnpCardType { get; set; }

    /// <summary>Ngày giờ thanh toán (từ VNPay, định dạng yyyyMMddHHmmss)</summary>
    public string? VnpPayDate { get; set; }

    /// <summary>Trạng thái giao dịch VNPay (00 = thành công)</summary>
    public string? VnpTransactionStatus { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    // ── Navigation ───────────────────────────────────────────────────────
    public User User { get; set; } = null!;

    public HousingProject? HousingProject { get; set; }

    public HousingApplication? HousingApplication { get; set; }
}
