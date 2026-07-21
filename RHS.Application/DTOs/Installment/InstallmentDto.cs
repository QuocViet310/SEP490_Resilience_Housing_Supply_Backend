namespace RHS.Application.DTOs.Installment;

/// <summary>
/// DTO hiển thị một đợt thanh toán cho FE.
/// </summary>
public class InstallmentDto
{
    public Guid Id { get; set; }

    public int PhaseOrder { get; set; }

    public string PhaseName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime DueDate { get; set; }

    /// <summary>PENDING | PAID | OVERDUE | CANCELLED</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime? PaidAt { get; set; }

    /// <summary>Số ngày còn lại (âm nếu quá hạn)</summary>
    public int RemainingDays { get; set; }

    public string? Note { get; set; }
}

/// <summary>
/// Tổng hợp lịch đóng tiền toàn bộ đợt cho một hồ sơ.
/// </summary>
public class InstallmentSummaryDto
{
    public Guid ApplicationId { get; set; }

    public string? ApartmentTypeName { get; set; }

    public double? ApartmentArea { get; set; }

    /// <summary>Giá căn hộ (VND)</summary>
    public decimal? ApartmentPrice { get; set; }

    /// <summary>Tổng tiền tất cả đợt (VND)</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Tổng đã đóng (VND)</summary>
    public decimal TotalPaid { get; set; }

    /// <summary>Tổng còn lại (VND)</summary>
    public decimal TotalRemaining { get; set; }

    public int TotalPhases { get; set; }

    public int PaidPhases { get; set; }

    public List<InstallmentDto> Phases { get; set; } = new();
}
