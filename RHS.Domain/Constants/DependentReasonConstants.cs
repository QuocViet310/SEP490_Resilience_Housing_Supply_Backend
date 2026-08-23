namespace RHS.Domain.Constants;

/// <summary>
/// Hằng số lý do được xếp vào diện người phụ thuộc trong hộ gia đình.
/// Theo quy định: Con dưới 18 tuổi, người đang theo học đại học/cao đẳng, hoặc người mất sức lao động/khuyết tật.
/// </summary>
public static class DependentReasonConstants
{
    /// <summary>Con dưới 18 tuổi (chưa đến tuổi lao động)</summary>
    public const string Under18 = "UNDER_18";

    /// <summary>Người đang theo học đại học / cao đẳng / học nghề chính quy</summary>
    public const string Student = "STUDENT";

    /// <summary>Người mất sức lao động / khuyết tật nặng / bệnh hiểm nghèo</summary>
    public const string Disabled = "DISABLED";

    /// <summary>Người già hết tuổi lao động / không có thu nhập</summary>
    public const string Elderly = "ELDERLY";

    /// <summary>Khác</summary>
    public const string Other = "OTHER";

    public static readonly IReadOnlyList<string> AllValues = new[]
    {
        Under18,
        Student,
        Disabled,
        Elderly,
        Other
    };

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Under18]  = "Con dưới 18 tuổi",
        [Student]  = "Học sinh / Sinh viên",
        [Disabled] = "Mất sức lao động / Khuyết tật",
        [Elderly]  = "Người già hết tuổi lao động",
        [Other]    = "Khác"
    };

    public static bool IsValid(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) && AllValues.Contains(reason.ToUpperInvariant());

    public static string GetLabel(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) && Labels.TryGetValue(reason.ToUpperInvariant(), out var label)
            ? label
            : reason ?? string.Empty;
}
