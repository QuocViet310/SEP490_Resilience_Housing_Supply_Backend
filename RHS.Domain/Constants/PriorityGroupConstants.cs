namespace RHS.Domain.Constants;

/// <summary>
/// Đối tượng thụ hưởng RHS: hộ nghèo / cận nghèo khu vực đô thị
/// (khoản 2, 3, 4 Điều 76 Luật Nhà ở — Đ30.3 không áp dụng trần thu nhập 15/30 triệu).
/// </summary>
public static class PriorityGroupConstants
{
    public const string UrbanPoor = "URBAN_POOR";
    public const string UrbanNearPoor = "URBAN_NEAR_POOR";

    public static readonly IReadOnlyList<string> AllValues = new[]
    {
        UrbanPoor,
        UrbanNearPoor
    };

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [UrbanPoor] = "Hộ nghèo đô thị",
        [UrbanNearPoor] = "Hộ cận nghèo đô thị"
    };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && AllValues.Contains(value);

    public static bool IsUrbanPoorOrNearPoor(string? value) => IsValid(value);
}
