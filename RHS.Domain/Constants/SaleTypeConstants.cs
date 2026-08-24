namespace RHS.Domain.Constants;

/// <summary>
/// Hình thức mở bán căn hộ NOXH.
/// </summary>
public static class SaleTypeConstants
{
    /// <summary>Sở hữu 100% (Khách hàng đứng tên sở hữu toàn bộ căn hộ)</summary>
    public const string FullOwnership = "FULL_OWNERSHIP";

    /// <summary>Đồng sở hữu (CĐT chia sẻ tỷ lệ sở hữu theo đề án dự án)</summary>
    public const string CoOwnership = "CO_OWNERSHIP";

    public static readonly IReadOnlyList<string> All = new[] { FullOwnership, CoOwnership };

    public static bool IsValid(string? saleType) =>
        !string.IsNullOrWhiteSpace(saleType) && All.Contains(saleType.Trim().ToUpperInvariant());

    public static string GetDisplayName(string? saleType) => saleType?.Trim().ToUpperInvariant() switch
    {
        FullOwnership => "Sở hữu toàn bộ (100%)",
        CoOwnership => "Đồng sở hữu",
        _ => saleType ?? string.Empty
    };
}
