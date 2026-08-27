using System.ComponentModel.DataAnnotations;
using RHS.Application.DTOs.Milestone;

namespace RHS.Application.DTOs.HousingProjects;

/// <summary>
/// DTO Tạo mới dự án Nhà Ở Xã Hội (Nhận chuỗi JSON thuần [FromBody]).
/// </summary>
public class CreateHousingProjectRequestDto
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

    /// <summary>Số quyết định phê duyệt chủ trương / quy hoạch (bắt buộc)</summary>
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

    /// <summary>Danh sách URL hình ảnh phối cảnh, tiện ích dự án</summary>
    public List<string>? Images { get; set; }

    /// <summary>
    /// Giá bán thấp nhất (VND) - Tùy chọn, hệ thống sẽ tự động đồng bộ khi thêm căn hộ.
    /// </summary>
    public decimal MinPrice { get; set; }

    /// <summary>
    /// Giá bán cao nhất (VND) - Tùy chọn, hệ thống sẽ tự động đồng bộ khi thêm căn hộ.
    /// </summary>
    public decimal MaxPrice { get; set; }

    /// <summary>
    /// Diện tích nhỏ nhất (m²) - Tùy chọn, hệ thống sẽ tự động đồng bộ khi thêm căn hộ.
    /// </summary>
    public double MinArea { get; set; }

    /// <summary>
    /// Diện tích lớn nhất (m²) - Tùy chọn, hệ thống sẽ tự động đồng bộ khi thêm căn hộ.
    /// </summary>
    public double MaxArea { get; set; }

    /// <summary>
    /// Tổng số căn hộ mở bán - Tùy chọn, hệ thống sẽ tự động đồng bộ khi thêm căn hộ.
    /// </summary>
    public int AvailableUnits { get; set; }

    /// <summary>
    /// CĐT sở hữu dự án.
    /// - Khi CĐT tạo dự án: BE tự động gán từ JWT token.
    /// - Khi Admin/SXD tạo hộ: Truyền UserId của CĐT.
    /// </summary>
    public Guid? DeveloperId { get; set; }

    /// <summary>
    /// Cấu hình linh hoạt từ 3 đến 6 đợt đóng tiền cho dự án (Theo đặc tả nghiệp vụ NOXH).
    /// - Bắt buộc: 3 đến 6 đợt.
    /// - Tổng %: Đúng bằng 100%.
    /// - Đợt 1: Tối đa 30% giá trị hợp đồng.
    /// - Đợt cuối (Sổ hồng): Giữ lại 5%.
    /// * Lưu ý: Nếu để trống (null), hệ thống sẽ tự động khởi tạo 5 đợt chuẩn theo tiến độ thi công NOXH.
    /// </summary>
    public List<MilestoneSetupItemDto>? Milestones { get; set; }
}
