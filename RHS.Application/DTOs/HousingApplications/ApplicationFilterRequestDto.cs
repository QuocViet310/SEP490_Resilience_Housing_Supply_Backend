using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.HousingApplications;

/// <summary>
/// Request DTO để lọc và phân trang danh sách hồ sơ.
/// Dùng cho cả Applicant (xem hồ sơ của mình) và Officer/Manager (xem tất cả).
/// </summary>
public class ApplicationFilterRequestDto
{
    /// <summary>Trang hiện tại (bắt đầu từ 1)</summary>
    [Range(1, int.MaxValue, ErrorMessage = "PageIndex phải >= 1.")]
    public int PageIndex { get; set; } = 1;

    /// <summary>Số lượng mỗi trang (mặc định 10, tối đa 50)</summary>
    [Range(1, 50, ErrorMessage = "PageSize phải từ 1 đến 50.")]
    public int PageSize { get; set; } = 10;

    /// <summary>Lọc theo trạng thái hồ sơ (ví dụ: SUBMITTED, UNDER_REVIEW)</summary>
    public string? Status { get; set; }

    /// <summary>Lọc theo ID dự án</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Tìm kiếm theo Họ tên hoặc Số CCCD của người đăng ký</summary>
    public string? Search { get; set; }

    /// <summary>Lọc theo ngày nộp hồ sơ (từ ngày)</summary>
    public DateTime? SubmittedFrom { get; set; }

    /// <summary>Lọc theo ngày nộp hồ sơ (đến ngày)</summary>
    public DateTime? SubmittedTo { get; set; }
}
