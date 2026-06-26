using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.Payment;

/// <summary>
/// DTO tạo yêu cầu thanh toán đặt cọc cho hồ sơ đã được duyệt (APPROVED).
/// Số tiền (Amount) sẽ tự động lấy từ DepositAmount của dự án.
/// </summary>
public class CreatePaymentDto
{
    /// <summary>
    /// ID hồ sơ đăng ký đã được phê duyệt (APPROVED).
    /// Hệ thống sẽ lấy Amount từ HousingProject.DepositAmount.
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
