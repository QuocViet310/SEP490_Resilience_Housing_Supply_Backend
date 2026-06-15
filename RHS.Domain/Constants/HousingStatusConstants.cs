namespace RHS.Domain.Constants;

/// <summary>
/// Hằng số mô tả thực trạng nhà ở của người đăng ký.
/// Dùng cho trường HousingApplication.HousingStatus.
/// </summary>
public static class HousingStatusConstants
{
    /// <summary>Chưa có nhà ở</summary>
    public const string NoHouse = "NO_HOUSE";

    /// <summary>Diện tích nhà ở dưới 15m²</summary>
    public const string SmallHouse = "SMALL_HOUSE";

    public static readonly IReadOnlyList<string> AllValues = new[]
    {
        NoHouse,
        SmallHouse
    };

    public static bool IsValid(string housingStatus) => AllValues.Contains(housingStatus);
}
