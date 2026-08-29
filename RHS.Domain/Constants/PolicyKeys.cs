using System.Collections.Generic;

namespace RHS.Domain.Constants;

/// <summary>
/// Key ổn định cho bảng PolicyConfig (Nghị định NOXH — mua/bán).
/// </summary>
public static class PolicyKeys
{
    public const string TacitApprovalDays = "TACIT_APPROVAL_DAYS";
    public const string DepositPaymentHours = "DEPOSIT_PAYMENT_HOURS";
    public const string MaxAreaPerPersonM2 = "MAX_AREA_PER_PERSON_M2";
    public const string IncomeSingleMaxVnd = "INCOME_SINGLE_MAX_VND";
    public const string IncomeMarriedMaxVnd = "INCOME_MARRIED_MAX_VND";
    public const string OneApplicationPerApplicant = "ONE_APPLICATION_PER_APPLICANT";
    public const string PublicAnnounceMinDays = "PUBLIC_ANNOUNCE_MIN_DAYS";
    public const string SxdCrosscheckSilenceDays = "SXD_CROSSCHECK_SILENCE_DAYS";
    public const string ContractSigningDeadlineDays = "CONTRACT_SIGNING_DEADLINE_DAYS";
    public const string LatePaymentPenaltyDailyRate = "LATE_PAYMENT_PENALTY_DAILY_RATE";
    public const string PriorityPointsTableJson = "PRIORITY_POINTS_TABLE_JSON";

    public static readonly IReadOnlyList<(string Key, string Value, string Category, string Description)> Defaults =
        new[]
        {
            (TacitApprovalDays, "20", "Automation",
                "Số ngày SXD im lặng trước khi tự động phê duyệt (Đ38.1.đ)."),
            (DepositPaymentHours, "168", "Automation",
                "Số giờ phải thanh toán đặt cọc sau khi ký hợp đồng nguyên tắc (CONTRACT_SIGNED), tính từ SignedAt. Mặc định 168 = 7 ngày."),
            (MaxAreaPerPersonM2, "10", "Eligibility",
                "Diện tích nhà ở bình quân đầu người tối đa (m²) — Đ29.2 Nghị định 100/2024 (< 10m²/người)."),
            (IncomeSingleMaxVnd, "15000000", "Eligibility",
                "Thu nhập tháng tối đa người độc thân (VND) — Đ30.1.a."),
            (IncomeMarriedMaxVnd, "30000000", "Eligibility",
                "Tổng thu nhập tháng tối đa vợ+chồng (VND) — Đ30.1.a."),
            (OneApplicationPerApplicant, "true", "Sales",
                "Mỗi người chỉ được nộp hồ sơ tại một dự án tại một thời điểm — Đ38.1.e."),
            (PublicAnnounceMinDays, "30", "Sales",
                "Số ngày công bố tối thiểu trước khi mở nhận hồ sơ — Đ38.1.b."),
            (SxdCrosscheckSilenceDays, "20", "Sales",
                "Số ngày SXD không phản hồi sau khi nhận danh sách (đồng bộ tacit approval) — Đ38.1.đ."),
            (ContractSigningDeadlineDays, "15", "Sales",
                "Số ngày hạn chót để người dân ký hợp đồng nguyên tắc kể từ khi vào CONTRACT_PENDING (ưu tiên / trúng bốc thăm)."),
            (LatePaymentPenaltyDailyRate, "0.0005", "Finance",
                "Tỷ lệ lãi suất phạt chậm nộp tiền đợt thanh toán (VND/ngày). Mặc định 0.0005 = 0.05%/ngày."),
            (PriorityPointsTableJson, "[{\"GroupCode\":\"MERIT_PERSON\",\"GroupName\":\"Người có công với cách mạng\",\"Points\":10,\"Description\":\"Điểm cộng ưu tiên người có công\"},{\"GroupCode\":\"URBAN_POOR\",\"GroupName\":\"Hộ nghèo, cận nghèo đô thị\",\"Points\":8,\"Description\":\"Điểm cộng ưu tiên hộ nghèo/cận nghèo\"},{\"GroupCode\":\"LOW_INCOME\",\"GroupName\":\"Người thu nhập thấp đô thị\",\"Points\":6,\"Description\":\"Điểm cộng ưu tiên người thu nhập thấp\"},{\"GroupCode\":\"INDUSTRIAL_WORKER\",\"GroupName\":\"Công nhân, người lao động KCN\",\"Points\":6,\"Description\":\"Điểm cộng ưu tiên công nhân KCN\"}]", "Eligibility",
                "Bảng cấu hình thang điểm ưu tiên cho các đối tượng thụ hưởng (JSON format).")
        };
}
