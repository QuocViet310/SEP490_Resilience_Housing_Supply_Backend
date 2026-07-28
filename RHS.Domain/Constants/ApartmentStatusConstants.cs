namespace RHS.Domain.Constants;

/// <summary>Trạng thái bàn giao căn hộ trong dự án.</summary>
public static class ApartmentStatusConstants
{
    public const string Available = "AVAILABLE";
    public const string Assigned = "ASSIGNED";

    public static readonly string[] All = { Available, Assigned };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status)
        && All.Contains(status, StringComparer.OrdinalIgnoreCase);
}
