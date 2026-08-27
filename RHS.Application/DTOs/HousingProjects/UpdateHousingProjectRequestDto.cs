using System.ComponentModel.DataAnnotations;
using RHS.Application.DTOs.Milestone;

namespace RHS.Application.DTOs.HousingProjects;

/// <summary>
/// DTO Cập nhật thông tin dự án Nhà Ở Xã Hội (Nhận chuỗi JSON thuần [FromBody]).
/// </summary>
public class UpdateHousingProjectRequestDto
{
    [Required(ErrorMessage = "Tên dự án là bắt buộc.")]
    [StringLength(250, MinimumLength = 3, ErrorMessage = "Tên dự án từ 3 đến 250 ký tự.")]
    public string ProjectName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tỉnh/Thành phố là bắt buộc.")]
    public string Province { get; set; } = string.Empty;

    [Required(ErrorMessage = "Quận/Huyện là bắt buộc.")]
    public string District { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phường/Xã là bắt buộc.")]
    public string Ward { get; set; } = string.Empty;

    [Required(ErrorMessage = "Địa chỉ đường/phố là bắt buộc.")]
    public string Street { get; set; } = string.Empty;

    /// <summary>Số quyết định phê duyệt chủ trương / quy hoạch</summary>
    [Required(ErrorMessage = "Số quyết định phê duyệt là bắt buộc.")]
    public string DecisionNumber { get; set; } = string.Empty;

    /// <summary>Thời gian mở tiếp nhận hồ sơ đăng ký mua nhà</summary>
    public DateTime? ApplicationOpenDate { get; set; }

    /// <summary>Thời hạn kết thúc tiếp nhận hồ sơ đăng ký mua nhà</summary>
    public DateTime? ApplicationCloseDate { get; set; }

    /// <summary>Thời gian tổ chức bốc thăm quyền mua căn hộ</summary>
    public DateTime? LotteryDate { get; set; }

    /// <summary>Địa điểm tổ chức bốc thăm</summary>
    public string? LotteryLocation { get; set; }

    /// <summary>URL ảnh đại diện (Thumbnail) của dự án</summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>Danh sách URL hình ảnh dự án</summary>
    public List<string>? Images { get; set; }

    /// <summary>Giá bán thấp nhất (VND)</summary>
    public decimal MinPrice { get; set; }

    /// <summary>Giá bán cao nhất (VND)</summary>
    public decimal MaxPrice { get; set; }

    /// <summary>Diện tích nhỏ nhất (m²)</summary>
    public double MinArea { get; set; }

    /// <summary>Diện tích lớn nhất (m²)</summary>
    public double MaxArea { get; set; }

    /// <summary>Tổng số căn hộ mở bán</summary>
    public int AvailableUnits { get; set; }

    /// <summary>
    /// Cấu hình lại các đợt đóng tiền (nếu muốn cập nhật đồng thời trong payload sửa dự án).
    /// </summary>
    public List<MilestoneSetupItemDto>? Milestones { get; set; }
}
