namespace RHS.Domain.Constants;

/// <summary>
/// Số đợt do chủ đầu tư thỏa thuận trong hợp đồng — luật không ấn định số đợt.
/// Min = 2 vì Điều 89 Luật Nhà ở 2023 bắt giữ lại ít nhất 5% đến khi cấp giấy chứng nhận.
/// Max là trần kỹ thuật để tránh gửi lịch quá dài.
/// </summary>
public static class PaymentPhaseCountConstants
{
    public const int Min = 2;
    public const int Max = 50;
}

/// <summary>
/// Trần tỷ lệ thanh toán nhà ở xã hội — Điều 89.1.c Luật Nhà ở 2023.
/// Số đợt do các bên thỏa thuận, phù hợp tiến độ xây dựng đã phê duyệt.
/// </summary>
public static class PaymentScheduleRules
{
    /// <summary>Ứng trước lần đầu (gồm tiền đặt cọc nếu có) ≤ 30%.</summary>
    public const decimal FirstPaymentMaxPercent = 30m;

    /// <summary>Tổng thu đến trước khi bàn giao nhà ≤ 70%.</summary>
    public const decimal BeforeHandoverMaxPercent = 70m;

    /// <summary>Tổng thu đến trước khi cấp giấy chứng nhận ≤ 95%.</summary>
    public const decimal BeforeCertificateMaxPercent = 95m;

    /// <summary>Phải giữ lại ít nhất 5% đến khi cấp giấy chứng nhận.</summary>
    public const decimal RetainedUntilCertificateMinPercent = 5m;

    public static bool IsBeforeHandoverTrigger(string? trigger)
    {
        if (string.IsNullOrWhiteSpace(trigger)) return true;
        var t = trigger.Trim().ToUpperInvariant();
        return t is TriggerEventConstants.OnLotteryWon
            or TriggerEventConstants.OnContractSigned
            or TriggerEventConstants.OnApproved
            or TriggerEventConstants.ConstructionRoughFloor
            or TriggerEventConstants.RoofingCompleted;
    }

    public static bool IsCertificateTrigger(string? trigger) =>
        string.Equals(trigger, TriggerEventConstants.RedBookIssued, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Thứ tự tiến độ thực tế: cấp nhà → ký hợp đồng → phần thô → cất nóc → bàn giao → sổ hồng.
    /// Được phép bỏ qua mốc, không được xếp ngược.
    /// </summary>
    public static int TriggerRank(string? trigger)
    {
        if (string.IsNullOrWhiteSpace(trigger)) return -1;
        return trigger.Trim().ToUpperInvariant() switch
        {
            TriggerEventConstants.OnLotteryWon => 0,
            TriggerEventConstants.OnApproved => 0,
            TriggerEventConstants.OnContractSigned => 1,
            TriggerEventConstants.ConstructionRoughFloor => 2,
            TriggerEventConstants.RoofingCompleted => 3,
            TriggerEventConstants.Handover => 4,
            TriggerEventConstants.RedBookIssued => 5,
            _ => -1
        };
    }

    public static void ValidateTriggerOrder(
        IReadOnlyList<(int PhaseOrder, string PhaseName, string TriggerEvent)> phases)
    {
        for (int i = 1; i < phases.Count; i++)
        {
            var prev = TriggerRank(phases[i - 1].TriggerEvent);
            var cur = TriggerRank(phases[i].TriggerEvent);
            if (prev < 0 || cur < 0) continue;
            if (cur < prev)
            {
                throw new ArgumentException(
                    $"Đợt {phases[i].PhaseOrder} đang gắn mốc sớm hơn Đợt {phases[i - 1].PhaseOrder}. " +
                    "Thứ tự mốc phải theo tiến độ: cấp nhà → ký hợp đồng → phần thô → cất nóc → bàn giao nhà → cấp sổ hồng.");
            }
        }
    }

    public static void ValidateRatios(
        IReadOnlyList<(int PhaseOrder, string PhaseName, decimal Percentage, string TriggerEvent)> phases)
    {
        decimal beforeHandover = 0m;
        decimal beforeCertificate = 0m;
        decimal certificate = 0m;

        foreach (var p in phases)
        {
            if (IsCertificateTrigger(p.TriggerEvent))
            {
                certificate += p.Percentage;
                continue;
            }

            beforeCertificate += p.Percentage;
            if (IsBeforeHandoverTrigger(p.TriggerEvent))
                beforeHandover += p.Percentage;
        }

        if (beforeHandover > BeforeHandoverMaxPercent + 0.001m)
        {
            throw new ArgumentException(
                $"Tổng các đợt trước thời điểm bàn giao đang là {beforeHandover:0.##}%. Điều 89 Luật Nhà ở năm 2023: không được thu quá 70% giá trị hợp đồng đến trước khi bàn giao nhà.");
        }

        if (beforeCertificate > BeforeCertificateMaxPercent + 0.001m)
        {
            throw new ArgumentException(
                $"Tổng các đợt trước khi cấp giấy chứng nhận đang là {beforeCertificate:0.##}%. Điều 89 Luật Nhà ở năm 2023: không được thu quá 95% đến trước khi cấp giấy chứng nhận.");
        }

        if (certificate + 0.001m < RetainedUntilCertificateMinPercent)
        {
            throw new ArgumentException(
                "Phải có đợt gắn mốc cấp giấy chứng nhận (sổ hồng) với tỷ lệ tối thiểu 5% giá trị hợp đồng (Điều 89 Luật Nhà ở năm 2023).");
        }
    }
}

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
    /// <summary>Khi hồ sơ được Sở Xây dựng phê duyệt (APPROVED) — legacy / tùy chọn</summary>
    public const string OnApproved = "ON_APPROVED";

    /// <summary>Khi trúng bốc thăm hoặc cấp nhà — mở đợt tiền cọc (Đợt 1).</summary>
    public const string OnLotteryWon = "ON_LOTTERY_WON";

    /// <summary>Mốc sau khi người dân ký hợp đồng. Chủ đầu tư mở đợt thủ công khi tiến độ thật tới — không tự mở lúc ký.</summary>
    public const string OnContractSigned = "ON_CONTRACT_SIGNED";

    /// <summary>Khi chủ đầu tư công bố hoàn thành phần thô.</summary>
    public const string ConstructionRoughFloor = "CONSTRUCTION_ROUGH_FLOOR";

    /// <summary>Khi chủ đầu tư công bố cất nóc công trình.</summary>
    public const string RoofingCompleted = "ROOFING_COMPLETED";

    /// <summary>Khi chủ đầu tư công bố bàn giao nhà.</summary>
    public const string Handover = "HANDOVER";

    /// <summary>Khi chủ đầu tư công bố cấp giấy chứng nhận (sổ hồng).</summary>
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
        OnLotteryWon => "Khi được cấp nhà hoặc trúng bốc thăm",
        OnContractSigned => "Sau khi ký hợp đồng mua bán",
        ConstructionRoughFloor => "Khi hoàn thành xây dựng phần thô",
        RoofingCompleted => "Khi cất nóc công trình",
        Handover => "Khi bàn giao nhà",
        RedBookIssued => "Khi cấp giấy chứng nhận (sổ hồng)",
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
