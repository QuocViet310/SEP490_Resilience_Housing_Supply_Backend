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

    public static readonly IReadOnlyList<(string Key, string Value, string Category, string Description)> Defaults =
        new[]
        {
            (TacitApprovalDays, "20", "Automation",
                "Số ngày SXD im lặng trước khi tự động phê duyệt (Đ38.1.đ)."),
            (DepositPaymentHours, "24", "Automation",
                "Số giờ phải thanh toán đặt cọc sau khi APPROVED."),
            (MaxAreaPerPersonM2, "15", "Eligibility",
                "Diện tích nhà ở bình quân đầu người tối đa (m²) — Đ29.2."),
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
        };
}
