namespace RHS.Domain.Entities;

/// <summary>
/// Khoản thu thực tế cho từng đợt, từng hồ sơ.
/// Sinh tự động từ PaymentMilestone khi sự kiện kích hoạt (trigger event) xảy ra.
/// Tách biệt hoàn toàn với Payment (VNPay transaction):
///   - PaymentInstallment = "what's owed" (khoản phải thu)
///   - Payment            = "how it's paid" (giao dịch thanh toán)
/// </summary>
public class PaymentInstallment
{
    public Guid Id { get; set; }

    /// <summary>Hồ sơ đăng ký liên kết</summary>
    public Guid ApplicationId { get; set; }

    /// <summary>Milestone template đã sinh ra khoản thu này</summary>
    public Guid MilestoneId { get; set; }

    /// <summary>Số tiền phải đóng (VND) — đã tính toán từ milestone</summary>
    public decimal Amount { get; set; }

    /// <summary>Ngày sự kiện kích hoạt (trigger event fired)</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Hạn chót thanh toán = StartDate + Milestone.DueDays</summary>
    public DateTime DueDate { get; set; }

    /// <summary>PENDING | PAID | OVERDUE | CANCELLED</summary>
    public string Status { get; set; } = "PENDING";

    /// <summary>Ngày thanh toán thực tế</summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>FK sang giao dịch VNPay khi đã thanh toán</summary>
    public Guid? PaymentId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public HousingApplication HousingApplication { get; set; } = null!;

    public PaymentMilestone Milestone { get; set; } = null!;

    public Payment? Payment { get; set; }
}
