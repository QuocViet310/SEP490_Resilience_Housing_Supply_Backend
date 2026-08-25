namespace RHS.Application.DTOs.Installment;

/// <summary>
/// DTO hiển thị một đợt thanh toán cho FE (bao gồm lãi phạt 0.05%/ngày khi trễ hạn).
/// </summary>
public class InstallmentDto
{
    public Guid Id { get; set; }

    public int PhaseOrder { get; set; }

    public string PhaseName { get; set; } = string.Empty;

    /// <summary>Số tiền gốc đợt thu (VND)</summary>
    public decimal Amount { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime DueDate { get; set; }

    /// <summary>PENDING | PAID | OVERDUE | CANCELLED</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime? PaidAt { get; set; }

    /// <summary>Số ngày còn lại (âm nếu quá hạn)</summary>
    public int RemainingDays { get; set; }

    /// <summary>Số ngày quá hạn (0 nếu chưa quá hạn)</summary>
    public int OverdueDays { get; set; }

    /// <summary>Mức tỷ lệ lãi phạt/ngày (0.0005m = 0.05%/ngày)</summary>
    public decimal DailyPenaltyRate { get; set; } = 0.0005m;

    /// <summary>Số tiền lãi phạt quá hạn tích lũy (VND)</summary>
    public decimal PenaltyAmount { get; set; }

    /// <summary>Tổng số tiền phải đóng = Gốc (Amount) + Lãi phạt (PenaltyAmount)</summary>
    public decimal TotalPayableAmount { get; set; }

    public string? Note { get; set; }
}

/// <summary>
/// Tổng hợp lịch đóng tiền toàn bộ đợt cho một hồ sơ (bao gồm lãi phạt quá hạn tích lũy).
/// </summary>
public class InstallmentSummaryDto
{
    public Guid ApplicationId { get; set; }

    public string? ApartmentTypeName { get; set; }

    public double? ApartmentArea { get; set; }

    /// <summary>Giá căn hộ (VND)</summary>
    public decimal? ApartmentPrice { get; set; }

    /// <summary>Tổng tiền gốc tất cả đợt (VND)</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Tổng tiền đã đóng (VND)</summary>
    public decimal TotalPaid { get; set; }

    /// <summary>Tổng tiền gốc còn lại chưa đóng (VND)</summary>
    public decimal TotalRemaining { get; set; }

    /// <summary>Tổng tiền lãi phạt quá hạn tích lũy (VND)</summary>
    public decimal TotalPenalty { get; set; }

    /// <summary>Tổng số tiền còn lại phải đóng bao gồm cả lãi phạt (VND)</summary>
    public decimal TotalAmountWithPenalty { get; set; }

    public int TotalPhases { get; set; }

    public int PaidPhases { get; set; }

    public List<InstallmentDto> Phases { get; set; } = new();
}
