using System.ComponentModel.DataAnnotations;
using RHS.Domain.Constants;

namespace RHS.Application.DTOs.HousingApplications;

/// <summary>
/// Request DTO để tạo mới hồ sơ đăng ký nhà ở xã hội.
/// Hồ sơ được tạo với trạng thái DRAFT (chưa nộp).
/// </summary>
public class CreateApplicationRequestDto
{
    /// <summary>ID dự án nhà ở muốn đăng ký</summary>
    [Required(ErrorMessage = "ProjectId là bắt buộc.")]
    public Guid ProjectId { get; set; }

    // ── Thông tin cá nhân ──────────────────────────────────────────

    /// <summary>Họ và tên đầy đủ</summary>
    [Required(ErrorMessage = "Họ tên là bắt buộc.")]
    [MaxLength(100, ErrorMessage = "Họ tên không được quá 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Số CCCD/CMND (9 hoặc 12 số)</summary>
    [Required(ErrorMessage = "Số CCCD là bắt buộc.")]
    [RegularExpression(@"^\d{9}(\d{3})?$", ErrorMessage = "Số CCCD phải là 9 hoặc 12 chữ số.")]
    public string CitizenId { get; set; } = string.Empty;

    /// <summary>Nghề nghiệp hiện tại (không bắt buộc)</summary>
    [MaxLength(200, ErrorMessage = "Nghề nghiệp không được quá 200 ký tự.")]
    public string? Occupation { get; set; }

    /// <summary>Nơi làm việc (không bắt buộc)</summary>
    [MaxLength(500, ErrorMessage = "Nơi làm việc không được quá 500 ký tự.")]
    public string? WorkPlace { get; set; }

    // ── Thông tin địa chỉ ─────────────────────────────────────────

    /// <summary>Nơi ở hiện tại (địa chỉ thực tế đang sinh sống)</summary>
    [Required(ErrorMessage = "Nơi ở hiện tại là bắt buộc.")]
    [MaxLength(500, ErrorMessage = "Địa chỉ không được quá 500 ký tự.")]
    public string CurrentResidence { get; set; } = string.Empty;

    /// <summary>Nơi đăng ký thường trú/tạm trú</summary>
    [Required(ErrorMessage = "Địa chỉ thường trú/tạm trú là bắt buộc.")]
    [MaxLength(500, ErrorMessage = "Địa chỉ không được quá 500 ký tự.")]
    public string PermanentAddress { get; set; } = string.Empty;

    // ── Thực trạng nhà ở & Thu nhập ───────────────────────────────

    /// <summary>
    /// Thực trạng nhà ở. Giá trị hợp lệ:
    /// "NO_HOUSE" (Chưa có nhà) hoặc "SMALL_HOUSE" (Diện tích &lt; 15m²).
    /// </summary>
    [Required(ErrorMessage = "Thực trạng nhà ở là bắt buộc.")]
    public string HousingStatus { get; set; } = string.Empty;

    /// <summary>Tình trạng hôn nhân</summary>
    [Required(ErrorMessage = "Tình trạng hôn nhân là bắt buộc.")]
    [MaxLength(50, ErrorMessage = "Tình trạng hôn nhân không được quá 50 ký tự.")]
    public string MaritalStatus { get; set; } = string.Empty;

    /// <summary>Số thành viên hộ gia đình</summary>
    [Required(ErrorMessage = "Số thành viên hộ gia đình là bắt buộc.")]
    [Range(1, 100, ErrorMessage = "Số thành viên hộ gia đình phải từ 1 trở lên.")]
    public int HouseholdMembersCount { get; set; }

    /// <summary>Thuộc đối tượng (Mẫu số 01 mục 8): hộ nghèo / cận nghèo đô thị</summary>
    [Required(ErrorMessage = "Đối tượng thụ hưởng là bắt buộc.")]
    [MaxLength(100, ErrorMessage = "Đối tượng không được quá 100 ký tự.")]
    public string PriorityGroup { get; set; } = string.Empty;

    /// <summary>Thu nhập (không bắt buộc — hộ nghèo/cận nghèo dùng chuẩn nghèo Đ30.3).</summary>
    [Range(0, 1_000_000_000, ErrorMessage = "Thu nhập không hợp lệ.")]
    public decimal? MonthlyIncome { get; set; }

    /// <summary>Thu nhập tháng của vợ/chồng (nếu có)</summary>
    [Range(0, 1_000_000_000, ErrorMessage = "Thu nhập vợ/chồng không hợp lệ.")]
    public decimal? SpouseMonthlyIncome { get; set; }

    /// <summary>Diện tích nhà ở bình quân đầu người (m²) — bắt buộc khi SMALL_HOUSE</summary>
    [Range(0, 1000, ErrorMessage = "Diện tích bình quân không hợp lệ.")]
    public decimal? AverageHousingAreaPerPerson { get; set; }
}
