namespace RHS.Domain.Constants;

/// <summary>
/// Phân nhóm căn hộ / Quỹ phân bổ NOXH.
/// </summary>
public static class UnitGroupConstants
{
    /// <summary>Căn Ưu Tiên (Quỹ căn dành cho đối tượng chính sách / điểm ưu tiên cao nhất)</summary>
    public const string Priority = "PRIORITY";

    /// <summary>Căn Tiêu Chuẩn (Quỹ căn mở rộng cho các đối tượng còn lại tham gia bốc thăm bình đẳng)</summary>
    public const string Standard = "STANDARD";

    public static readonly IReadOnlyList<string> All = new[] { Priority, Standard };

    public static bool IsValid(string? group) =>
        !string.IsNullOrWhiteSpace(group) && All.Contains(group.Trim().ToUpperInvariant());

    public static string GetDisplayName(string? group) => group?.Trim().ToUpperInvariant() switch
    {
        Priority => "Căn Hộ Ưu Tiên",
        Standard => "Căn Hộ Tiêu Chuẩn",
        _ => group ?? string.Empty
    };
}
