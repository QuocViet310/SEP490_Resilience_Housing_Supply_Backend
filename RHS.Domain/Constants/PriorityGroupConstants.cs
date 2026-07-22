namespace RHS.Domain.Constants;

/// <summary>
/// Nhóm đối tượng thụ hưởng chính sách nhà ở xã hội
/// theo Điều 76 Luật Nhà ở 2023.
/// Mỗi nhóm đối tượng cần giấy tờ chứng minh khác nhau.
/// </summary>
public static class PriorityGroupConstants
{
    // ── Nhóm 1: Người có công với cách mạng (khoản 1 Đ76) ──
    public const string MeritPerson = "MERIT_PERSON";

    // ── Nhóm 2-3: Hộ nghèo / cận nghèo khu vực nông thôn (khoản 2, 3 Đ76) ──
    public const string RuralPoor = "RURAL_POOR";
    public const string RuralNearPoor = "RURAL_NEAR_POOR";

    // ── Nhóm 4: Hộ nghèo / cận nghèo khu vực đô thị (khoản 4 Đ76) ──
    public const string UrbanPoor = "URBAN_POOR";
    public const string UrbanNearPoor = "URBAN_NEAR_POOR";

    // ── Nhóm 5: Người thu nhập thấp đô thị (khoản 5 Đ76) ──
    public const string LowIncomeUrban = "LOW_INCOME_URBAN";

    // ── Nhóm 6: Công nhân, người lao động tại DN/HTX/KCN (khoản 6 Đ76) ──
    public const string Worker = "WORKER";

    // ── Nhóm 7: Lực lượng vũ trang, cơ yếu (khoản 7 Đ76) ──
    public const string MilitaryPersonnel = "MILITARY_PERSONNEL";

    // ── Nhóm 8: Cán bộ, công chức, viên chức (khoản 8 Đ76) ──
    public const string CivilServant = "CIVIL_SERVANT";

    // ── Nhóm 9: Đối tượng trả lại nhà công vụ (khoản 9 Đ76) ──
    public const string PublicHousingReturn = "PUBLIC_HOUSING_RETURN";

    // ── Nhóm 10: Bị thu hồi đất / giải tỏa (khoản 10 Đ76) ──
    public const string LandRecoveryAffected = "LAND_RECOVERY_AFFECTED";

    /// <summary>Tất cả giá trị hợp lệ.</summary>
    public static readonly IReadOnlyList<string> AllValues = new[]
    {
        MeritPerson,
        RuralPoor,
        RuralNearPoor,
        UrbanPoor,
        UrbanNearPoor,
        LowIncomeUrban,
        Worker,
        MilitaryPersonnel,
        CivilServant,
        PublicHousingReturn,
        LandRecoveryAffected
    };

    /// <summary>Label tiếng Việt cho FE hiển thị.</summary>
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [MeritPerson]          = "Người có công với cách mạng",
        [RuralPoor]            = "Hộ nghèo nông thôn",
        [RuralNearPoor]        = "Hộ cận nghèo nông thôn",
        [UrbanPoor]            = "Hộ nghèo đô thị",
        [UrbanNearPoor]        = "Hộ cận nghèo đô thị",
        [LowIncomeUrban]       = "Người thu nhập thấp tại đô thị",
        [Worker]               = "Công nhân, người lao động tại DN/HTX/KCN",
        [MilitaryPersonnel]    = "Lực lượng vũ trang, cơ yếu",
        [CivilServant]         = "Cán bộ, công chức, viên chức",
        [PublicHousingReturn]  = "Đối tượng trả lại nhà công vụ",
        [LandRecoveryAffected] = "Bị thu hồi đất / giải tỏa nhà ở"
    };

    /// <summary>Các nhóm hộ nghèo / cận nghèo (dùng chuẩn nghèo, không xét trần thu nhập).</summary>
    public static readonly IReadOnlyList<string> PovertyGroups = new[]
    {
        RuralPoor, RuralNearPoor, UrbanPoor, UrbanNearPoor
    };

    /// <summary>Các nhóm cần xét trần thu nhập (Đ30.1 / Đ30.2).</summary>
    public static readonly IReadOnlyList<string> IncomeCheckedGroups = new[]
    {
        LowIncomeUrban, Worker, MilitaryPersonnel, CivilServant, PublicHousingReturn, LandRecoveryAffected
    };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && AllValues.Contains(value);

    public static bool IsUrbanPoorOrNearPoor(string? value) =>
        value == UrbanPoor || value == UrbanNearPoor;

    public static bool IsPovertyGroup(string? value) =>
        !string.IsNullOrWhiteSpace(value) && PovertyGroups.Contains(value);

    public static bool RequiresIncomeCheck(string? value) =>
        !string.IsNullOrWhiteSpace(value) && IncomeCheckedGroups.Contains(value);
}
