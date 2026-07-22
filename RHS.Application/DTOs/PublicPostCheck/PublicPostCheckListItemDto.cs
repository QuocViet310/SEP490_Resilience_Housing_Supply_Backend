namespace RHS.Application.DTOs.PublicPostCheck;

/// <summary>
/// DTO thông tin đối tượng mua trúng nhà ở xã hội hiển thị công khai trên Public Portal
/// Phục vụ toàn dân giám sát và chống sang nhượng trái quy định (Nghị định 100).
/// </summary>
public class PublicPostCheckListItemDto
{
    public Guid ApplicationId { get; set; }

    /// <summary>
    /// Họ và tên người trúng mua NOXH
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Số CCCD đã che mờ thông tin cá nhân (VD: 001093******)
    /// </summary>
    public string MaskedCitizenId { get; set; } = string.Empty;

    /// <summary>
    /// ID dự án nhà ở xã hội
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Tên dự án nhà ở xã hội
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Tỉnh/Thành phố dự án
    /// </summary>
    public string Province { get; set; } = string.Empty;

    /// <summary>
    /// Quận/Huyện dự án
    /// </summary>
    public string District { get; set; } = string.Empty;

    /// <summary>
    /// Mã căn / Mã suất trúng bốc thăm
    /// </summary>
    public string? SlotCode { get; set; }

    /// <summary>
    /// Kết quả bốc thăm (WON / PRIORITY_WON)
    /// </summary>
    public string? LotteryResult { get; set; }

    /// <summary>
    /// Số thành viên hộ gia đình
    /// </summary>
    public int HouseholdMembersCount { get; set; }

    /// <summary>
    /// Nhóm đối tượng ưu tiên (code)
    /// </summary>
    public string? PriorityGroup { get; set; }

    /// <summary>
    /// Tên mô tả nhóm đối tượng ưu tiên
    /// </summary>
    public string? PriorityGroupLabel { get; set; }

    /// <summary>
    /// Ngày ra quyết định duyệt / giao dịch đặt cọc thành công
    /// </summary>
    public DateTime? FinalDecisionDate { get; set; }

    /// <summary>
    /// Mốc thời gian đủ điều kiện sang nhượng/chuyển nhượng (5 năm theo NĐ 100)
    /// </summary>
    public DateTime? TransferEligibleDate { get; set; }

    /// <summary>
    /// Trạng thái có đang thuộc thời gian cấm sang nhượng hay không (true = Đang bị cấm)
    /// </summary>
    public bool IsUnderTransferRestriction { get; set; }

    /// <summary>
    /// Văn bản mô tả trạng thái cấm sang nhượng (VD: 🔴 Cấm chuyển nhượng - Còn 3 năm 2 tháng)
    /// </summary>
    public string RestrictionStatusText { get; set; } = string.Empty;
}
