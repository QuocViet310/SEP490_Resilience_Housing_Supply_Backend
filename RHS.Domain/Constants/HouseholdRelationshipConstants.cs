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

    public static bool IsValid(string value)
        => AllValues.Contains(value, StringComparer.OrdinalIgnoreCase);
}
