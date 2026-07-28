using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.Payment;

/// <summary>
/// DTO tạo yêu cầu thanh toán Đợt 1 (20% giá căn) sau khi ký HĐ.
/// Số tiền lấy từ PaymentInstallment Phase 1 (hoặc tính 20% giá căn).
/// </summary>
public class CreatePaymentDto
{
    /// <summary>
    /// ID hồ sơ đã ký hợp đồng (CONTRACT_SIGNED).
    /// </summary>
    [Required(ErrorMessage = "ApplicationId là bắt buộc")]
    public Guid ApplicationId { get; set; }

    /// <summary>
    /// Mô tả nội dung thanh toán (tùy chọn).
    /// Nếu để trống, hệ thống sẽ tự tạo: "Dat coc ho so {OrderId} - Du an {ProjectName}"
    /// </summary>
    [MaxLength(255, ErrorMessage = "Nội dung không được vượt quá 255 ký tự")]
    public string? OrderInfo { get; set; }
}
