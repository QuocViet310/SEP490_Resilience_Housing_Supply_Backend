namespace RHS.Domain.Constants;

/// <summary>
/// Hằng số mô tả tình trạng hôn nhân của công dân theo quy định pháp lý.
/// </summary>
public static class MaritalStatusConstants
{
    /// <summary>Độc thân</summary>
    public const string Single = "SINGLE";

    /// <summary>Đã kết hôn</summary>
    public const string Married = "MARRIED";

    /// <summary>Đã ly hôn</summary>
    public const string Divorced = "DIVORCED";

    public static readonly IReadOnlyList<string> AllValues = new[]
    {
        Single,
        Married,
        Divorced
    };

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Single]   = "Độc thân",
        [Married]  = "Đã kết hôn",
        [Divorced] = "Đã ly hôn"
    };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) && AllValues.Contains(status.ToUpperInvariant());

    public static string GetLabel(string? status) =>
        !string.IsNullOrWhiteSpace(status) && Labels.TryGetValue(status.ToUpperInvariant(), out var label)
            ? label
            : status ?? string.Empty;
}
