namespace RHS.Domain.Constants;

/// <summary>
/// Định nghĩa các loại căn hộ trong dự án Nhà ở xã hội.
/// Hỗ trợ 2 loại chuẩn chính: 1 Phòng ngủ (ONE_BEDROOM) và 2 Phòng ngủ (TWO_BEDROOM).
/// </summary>
public static class ApartmentTypeConstants
{
    public const string OneBedroom = "ONE_BEDROOM";
    public const string TwoBedroom = "TWO_BEDROOM";

    public static readonly IReadOnlyList<string> All = new[]
    {
        OneBedroom,
        TwoBedroom
    };

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [OneBedroom] = "Căn hộ 1 phòng ngủ",
        [TwoBedroom] = "Căn hộ 2 phòng ngủ"
    };

    public static bool IsValid(string? type) =>
        !string.IsNullOrWhiteSpace(type)
        && All.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string GetLabel(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return string.Empty;
        var normalized = type.Trim().ToUpperInvariant();
        return Labels.TryGetValue(normalized, out var label) ? label : type;
    }
}
