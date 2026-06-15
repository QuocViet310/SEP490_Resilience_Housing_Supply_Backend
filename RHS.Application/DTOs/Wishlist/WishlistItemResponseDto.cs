namespace RHS.Application.DTOs.Wishlist;

/// <summary>
/// DTO đại diện cho một mục trong danh sách yêu thích của người dùng.
/// Gộp thông tin wishlist (AddedAt) và thông tin tóm tắt của HousingProject.
/// </summary>
public class WishlistItemResponseDto
{
    /// <summary>ID của bản ghi Wishlist.</summary>
    public Guid WishlistId { get; set; }

    /// <summary>Thời điểm người dùng thêm dự án vào wishlist (UTC).</summary>
    public DateTime AddedAt { get; set; }

    // ── Thông tin HousingProject ──────────────────────────────────────────

    public Guid ProjectId { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Province { get; set; } = string.Empty;

    public string District { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public decimal MinPrice { get; set; }

    public decimal MaxPrice { get; set; }

    public double MinArea { get; set; }

    public double MaxArea { get; set; }

    public int AvailableUnits { get; set; }

    public string? ThumbnailUrl { get; set; }

    /// <summary>Tên trạng thái dự án (ví dụ: "Đang mở đăng ký").</summary>
    public string? Status { get; set; }
}
