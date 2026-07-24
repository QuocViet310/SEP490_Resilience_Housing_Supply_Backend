namespace RHS.Domain.Constants;

/// <summary>Phương thức tính số tiền cho milestone.</summary>
public static class CalculationTypeConstants
{
    /// <summary>Số tiền cố định (vd: cọc 50 triệu, dùng khi chưa biết loại căn)</summary>
    public const string FixedAmount = "FIXED_AMOUNT";

    /// <summary>Phần trăm trên giá căn hộ (dùng khi đã biết loại căn sau bốc thăm)</summary>
    public const string Percentage = "PERCENTAGE";

    public static readonly IReadOnlyList<string> All = new[] { FixedAmount, Percentage };

    public static bool IsValid(string type) => All.Contains(type);
}

/// <summary>Sự kiện kích hoạt sinh PaymentInstallment từ milestone template.</summary>
public static class TriggerEventConstants
{
    /// <summary>Khi hồ sơ được SXD phê duyệt (APPROVED) — legacy / tùy chọn</summary>
    public const string OnApproved = "ON_APPROVED";

    /// <summary>Khi trúng bốc thăm (WON/PRIORITY_WON) hoặc chốt danh sách + đã gán loại căn → đợt 1, đợt 2</summary>
    public const string OnLotteryWon = "ON_LOTTERY_WON";

    /// <summary>Khi người dân ký hợp đồng nguyên tắc → đợt đặt cọc</summary>
    public const string OnContractSigned = "ON_CONTRACT_SIGNED";

    public static readonly IReadOnlyList<string> All =
        new[] { OnApproved, OnLotteryWon, OnContractSigned };

    public static bool IsValid(string triggerEvent) => All.Contains(triggerEvent);
}

/// <summary>Trạng thái khoản thu (PaymentInstallment).</summary>
public static class InstallmentStatusConstants
{
    public const string Pending = "PENDING";
    public const string Paid = "PAID";
    public const string Overdue = "OVERDUE";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All =
        new[] { Pending, Paid, Overdue, Cancelled };
}
