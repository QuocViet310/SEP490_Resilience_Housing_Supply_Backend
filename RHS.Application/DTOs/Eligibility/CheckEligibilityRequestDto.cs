using System.ComponentModel.DataAnnotations;
using RHS.Application.DTOs.HouseholdMember;

namespace RHS.Application.DTOs.Eligibility;

/// <summary>
/// DTO gửi lên để kiểm tra nhanh điều kiện mua nhà ở xã hội (Pre-check) trước khi nộp đơn.
/// Nếu không truyền các trường thông tin cụ thể, hệ thống sẽ tự động dùng thông tin từ Profile của công dân.
/// </summary>
public class CheckEligibilityRequestDto
{
    /// <summary>ID dự án muốn đăng ký (tùy chọn)</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Nhóm đối tượng ưu tiên: URBAN_POOR, LOW_INCOME, INDUSTRIAL_WORKER, MERIT_PERSON...</summary>
    public string? PriorityGroup { get; set; }

    /// <summary>Tình trạng hôn nhân: SINGLE, MARRIED, DIVORCED</summary>
    public string? MaritalStatus { get; set; }

    /// <summary>Thu nhập hàng tháng của người đứng đơn (VND/tháng)</summary>
    [Range(0, 1000000000, ErrorMessage = "Thu nhập không hợp lệ.")]
    public decimal? MonthlyIncome { get; set; }

    /// <summary>Thu nhập hàng tháng của vợ/chồng (nếu đã kết hôn - VND/tháng)</summary>
    [Range(0, 1000000000, ErrorMessage = "Thu nhập vợ/chồng không hợp lệ.")]
    public decimal? SpouseMonthlyIncome { get; set; }

    /// <summary>Thực trạng nhà ở: NO_HOUSE (Chưa có nhà) | SMALL_HOUSE (Nhà chật &lt; 10m²/người)</summary>
    public string? HousingStatus { get; set; }

    /// <summary>Diện tích nhà ở bình quân đầu người (m²/người)</summary>
    [Range(0, 1000, ErrorMessage = "Diện tích bình quân không hợp lệ.")]
    public decimal? AverageHousingAreaPerPerson { get; set; }

    /// <summary>Tổng diện tích nhà ở hiện có (m²) nếu muốn hệ thống tự tính diện tích bình quân</summary>
    [Range(0, 10000, ErrorMessage = "Tổng diện tích nhà không hợp lệ.")]
    public double? TotalHousingArea { get; set; }

    /// <summary>Danh sách thành viên hộ gia đình (nếu có)</summary>
    public List<HouseholdMemberRequestDto>? HouseholdMembers { get; set; }

    /// <summary>
    /// Nếu true: Lấy dữ liệu từ Profile của user để điền các trường còn thiếu.
    /// Mặc định: true.
    /// </summary>
    public bool UseProfileFallback { get; set; } = true;
}
