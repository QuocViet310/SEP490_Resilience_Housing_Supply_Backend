namespace RHS.Domain.Entities;

/// <summary>
/// Thành viên trong hộ gia đình của người đăng ký nhà ở xã hội.
/// Mỗi HousingApplication có 0–N thành viên (không tính chủ hộ/người đứng đơn).
/// Dùng để đối chiếu trùng lặp CCCD giữa các hồ sơ.
/// </summary>
public class HouseholdMember
{
    public Guid MemberId { get; set; }

    public Guid ApplicationId { get; set; }

    /// <summary>Họ và tên thành viên</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Số CCCD/CMND của thành viên.
    /// Bắt buộc nếu thành viên từ 14 tuổi trở lên (theo luật Việt Nam).
    /// Nullable cho trẻ em dưới 14 tuổi.
    /// </summary>
    public string? CitizenId { get; set; }

    /// <summary>Ngày sinh của thành viên</summary>
    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// Quan hệ với người đứng đơn (chủ hộ).
    /// Sử dụng HouseholdRelationshipConstants.
    /// Ví dụ: SPOUSE, CHILD, PARENT, SIBLING, GRANDPARENT, GRANDCHILD, OTHER
    /// </summary>
    public string Relationship { get; set; } = string.Empty;

    /// <summary>Ghi chú bổ sung (ví dụ: "con gái 5 tuổi chưa có CCCD")</summary>
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Navigation properties
    // ─────────────────────────────────────────────────────────────

    public HousingApplication HousingApplication { get; set; } = null!;
}
