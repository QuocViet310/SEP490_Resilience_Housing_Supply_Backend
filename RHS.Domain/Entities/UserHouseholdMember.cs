namespace RHS.Domain.Entities;

/// <summary>
/// Thành viên trong hộ gia đình lưu trong Hồ sơ cá nhân (Citizen Profile) của User.
/// Dùng để lưu trữ danh sách nhân khẩu tái sử dụng cho các lần nộp hồ sơ NOXH.
/// </summary>
public class UserHouseholdMember
{
    public Guid MemberId { get; set; }

    public Guid UserId { get; set; }

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
    /// Quan hệ với chủ tài khoản/chủ hộ (SPOUSE, CHILD, PARENT, SIBLING, GRANDPARENT, GRANDCHILD, OTHER).
    /// Sử dụng HouseholdRelationshipConstants.
    /// </summary>
    public string Relationship { get; set; } = string.Empty;

    /// <summary>Nghề nghiệp / Công việc hiện tại</summary>
    public string? Occupation { get; set; }

    /// <summary>Thu nhập hàng tháng (VNĐ) của thành viên nếu trong độ tuổi lao động</summary>
    public decimal? MonthlyIncome { get; set; }

    /// <summary>
    /// Đánh dấu là người phụ thuộc (con dưới 18 tuổi, sinh viên, người mất sức lao động).
    /// Người phụ thuộc không tính thu nhập vào tổng thu nhập hộ nhưng được tính vào nhân khẩu để xét diện tích.
    /// </summary>
    public bool IsDependent { get; set; } = false;

    /// <summary>
    /// Lý do người phụ thuộc (UNDER_18, STUDENT, DISABLED, ELDERLY, OTHER).
    /// Sử dụng DependentReasonConstants.
    /// </summary>
    public string? DependentReason { get; set; }

    /// <summary>
    /// Đánh dấu thành viên có công với cách mạng hoặc thân nhân liệt sĩ.
    /// Dùng để cộng điểm ưu tiên thành viên trong hộ gia đình (theo mục 3 Đề án NOXH).
    /// </summary>
    public bool HasMeritService { get; set; } = false;

    /// <summary>Chi tiết diện người có công / thân nhân liệt sĩ</summary>
    public string? MeritDetails { get; set; }

    /// <summary>Ghi chú bổ sung</summary>
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Navigation properties
    // ─────────────────────────────────────────────────────────────

    public User User { get; set; } = null!;
}
