namespace RHS.Domain.Constants;

/// <summary>
/// Quan hệ của thành viên hộ gia đình với người đứng đơn (chủ hộ).
/// </summary>
public static class HouseholdRelationshipConstants
{
    public const string Spouse      = "SPOUSE";
    public const string Child       = "CHILD";
    public const string Parent      = "PARENT";
    public const string Sibling     = "SIBLING";
    public const string Grandparent = "GRANDPARENT";
    public const string Grandchild  = "GRANDCHILD";
    public const string Other       = "OTHER";

    public static readonly string[] AllValues =
    {
        Spouse, Child, Parent, Sibling, Grandparent, Grandchild, Other
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && AllValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Spouse]      = "Vợ / Chồng",
        [Child]       = "Con",
        [Parent]      = "Cha / Mẹ",
        [Sibling]     = "Anh / Chị / Em",
        [Grandparent] = "Ông / Bà",
        [Grandchild]  = "Cháu",
        [Other]       = "Khác"
    };

    public static string GetLabel(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Labels.TryGetValue(value.Trim().ToUpperInvariant(), out var label)
            ? label
            : value ?? string.Empty;
}
