namespace RHS.Domain.Constants;

/// <summary>
/// Hướng cửa chính & hướng ban công của căn hộ.
/// </summary>
public static class DirectionConstants
{
    public const string East = "EAST";
    public const string West = "WEST";
    public const string South = "SOUTH";
    public const string North = "NORTH";
    public const string SouthEast = "SOUTH_EAST";
    public const string NorthEast = "NORTH_EAST";
    public const string SouthWest = "SOUTH_WEST";
    public const string NorthWest = "NORTH_WEST";

    public static readonly IReadOnlyList<string> All = new[]
    {
        East, West, South, North,
        SouthEast, NorthEast, SouthWest, NorthWest
    };

    public static bool IsValid(string? direction) =>
        !string.IsNullOrWhiteSpace(direction) && All.Contains(direction.Trim().ToUpperInvariant());

    public static string GetDisplayName(string? direction) => direction?.Trim().ToUpperInvariant() switch
    {
        East => "Đông",
        West => "Tây",
        South => "Nam",
        North => "Bắc",
        SouthEast => "Đông Nam",
        NorthEast => "Đông Bắc",
        SouthWest => "Tây Nam",
        NorthWest => "Tây Bắc",
        _ => direction ?? string.Empty
    };
}
