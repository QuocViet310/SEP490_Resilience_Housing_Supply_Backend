namespace RHS.Domain.Entities;

/// <summary>
/// Đại diện cho một mục trong danh sách yêu thích (wishlist) của người dùng.
/// Mỗi bản ghi liên kết một User với một HousingProject mà họ đã lưu.
/// Ràng buộc duy nhất (UserId, HousingProjectId) đảm bảo không thêm trùng.
/// </summary>
public class Wishlist
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid HousingProjectId { get; set; }

    /// <summary>Thời điểm người dùng thêm dự án vào wishlist (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public User User { get; set; } = null!;
    public HousingProject HousingProject { get; set; } = null!;
}
