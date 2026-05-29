using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.Payment;

public class CreatePaymentDto
{
    /// <summary>
    /// Số tiền thanh toán (VND). Ví dụ: 100000 = 100,000 VND
    /// </summary>
    [Required(ErrorMessage = "Số tiền là bắt buộc")]
    [Range(1000, 100_000_000, ErrorMessage = "Số tiền phải từ 1,000 đến 100,000,000 VND")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Mô tả nội dung thanh toán. Ví dụ: "Thanh toan le phi ho so nha o"
    /// </summary>
    [Required(ErrorMessage = "Nội dung thanh toán là bắt buộc")]
    [MaxLength(255, ErrorMessage = "Nội dung không được vượt quá 255 ký tự")]
    public string OrderInfo { get; set; } = string.Empty;

    /// <summary>
    /// Loại đơn hàng. Mặc định: "other"
    /// </summary>
    public string OrderType { get; set; } = "other";
}
