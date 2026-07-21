namespace RHS.Domain.Entities;

/// <summary>
/// Template lịch thanh toán do CĐT cấu hình theo từng dự án.
/// Mỗi milestone định nghĩa: phương thức tính tiền, sự kiện kích hoạt, số ngày đáo hạn.
/// Khi sự kiện xảy ra, hệ thống sinh PaymentInstallment tương ứng cho từng hồ sơ.
/// </summary>
public class PaymentMilestone
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    /// <summary>Thứ tự đợt thanh toán (1, 2, 3...)</summary>
    public int PhaseOrder { get; set; }

    /// <summary>Tên hiển thị: "Đặt cọc", "Đợt 1", "Đợt 2"</summary>
    public string PhaseName { get; set; } = string.Empty;

    /// <summary>
    /// Phương thức tính số tiền:
    /// FIXED_AMOUNT = dùng FixedAmount (vd: cọc 50 triệu cố định, dùng khi chưa biết loại căn)
    /// PERCENTAGE   = dùng Percentage × giá căn hộ (dùng khi đã biết loại căn)
    /// </summary>
    public string CalculationType { get; set; } = string.Empty;

    /// <summary>Số tiền cố định (VND). Dùng khi CalculationType = FIXED_AMOUNT</summary>
    public decimal? FixedAmount { get; set; }

    /// <summary>Phần trăm trên giá căn hộ. Dùng khi CalculationType = PERCENTAGE</summary>
    public decimal? Percentage { get; set; }

    /// <summary>
    /// Sự kiện kích hoạt bắt đầu tính ngày đáo hạn.
    /// Ví dụ: ON_APPROVED, ON_LOTTERY_WON, ON_CONTRACT_SIGNED
    /// </summary>
    public string TriggerEvent { get; set; } = string.Empty;

    /// <summary>Số ngày đáo hạn kể từ khi sự kiện kích hoạt</summary>
    public int DueDays { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public HousingProject HousingProject { get; set; } = null!;

    public ICollection<PaymentInstallment> Installments { get; set; } = new List<PaymentInstallment>();
}
