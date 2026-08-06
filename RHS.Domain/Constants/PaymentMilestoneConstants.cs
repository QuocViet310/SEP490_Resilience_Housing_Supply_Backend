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

    /// <summary>Khi trúng bốc thăm (WON/PRIORITY_WON) hoặc cấp nhà → Đợt 1 (Cọc - 10%)</summary>
    public const string OnLotteryWon = "ON_LOTTERY_WON";

    /// <summary>Khi người dân ký hợp đồng mua bán chính thức → Đợt 2 (20%)</summary>
    public const string OnContractSigned = "ON_CONTRACT_SIGNED";

    /// <summary>Khi CĐT chuyển trạng thái xây dựng xong tầng thô → Đợt 3 (20%)</summary>
    public const string ConstructionRoughFloor = "CONSTRUCTION_ROUGH_FLOOR";

    /// <summary>Khi CĐT chuyển trạng thái cất nóc tòa nhà → Đợt 4 (20%)</summary>
    public const string RoofingCompleted = "ROOFING_COMPLETED";

    /// <summary>Khi CĐT chuyển trạng thái bàn giao căn hộ → Đợt 5 (25% + 2% Phí bảo trì)</summary>
    public const string Handover = "HANDOVER";

    /// <summary>Khi CĐT chuyển trạng thái nhận sổ hồng → Đợt 6 (5% còn lại)</summary>
    public const string RedBookIssued = "RED_BOOK_ISSUED";

    public static readonly IReadOnlyList<string> All = new[]
    {
        OnApproved,
        OnLotteryWon,
        OnContractSigned,
        ConstructionRoughFloor,
        RoofingCompleted,
        Handover,
        RedBookIssued
    };

    public static bool IsValid(string triggerEvent) => All.Contains(triggerEvent);

    /// <summary>Map mã thời điểm phát hành sang tên hiển thị tiếng Việt</summary>
    public static string GetDisplayName(string triggerEvent) => triggerEvent switch
    {
        OnLotteryWon => "Đợt 1 (Cọc - Trúng bốc thăm/Cấp nhà)",
        OnContractSigned => "Đợt 2 (Ký Hợp đồng mua bán)",
        ConstructionRoughFloor => "Đợt 3 (Xây dựng xong tầng thô)",
        RoofingCompleted => "Đợt 4 (Cất nóc tòa nhà)",
        Handover => "Đợt 5 (Bàn giao nhà & Chìa khóa)",
        RedBookIssued => "Đợt 6 (Nhận Giấy chứng nhận - Sổ hồng)",
        _ => triggerEvent
    };
}

/// <summary>Trạng thái khoản thu (PaymentInstallment).</summary>
public static class InstallmentStatusConstants
{
    public const string Locked = "LOCKED";
    public const string Pending = "PENDING";
    public const string Paid = "PAID";
    public const string Overdue = "OVERDUE";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Locked, Pending, Paid, Overdue, Cancelled
    };
}
