using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.CitizenProfile;

/// <summary>
/// DTO để cập nhật Hồ sơ cá nhân (Hôn nhân, Thu nhập, Nơi ở, Đối tượng ưu tiên).
/// Lưu ý: Nếu tài khoản đã xác minh eKYC, các trường Họ tên, CCCD, Ngày sinh sẽ được bảo vệ và chỉ cập nhật qua luồng eKYC.
/// </summary>
public class UpdateCitizenProfileDto
{
    // ── Thông tin cá nhân cơ bản ──────────────────────────────────
    [MaxLength(100, ErrorMessage = "Họ và tên không được vượt quá 100 ký tự.")]
    public string? FullName { get; set; }

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [MaxLength(15, ErrorMessage = "Số điện thoại không được vượt quá 15 ký tự.")]
    public string? PhoneNumber { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(20, ErrorMessage = "Số CCCD không được vượt quá 20 ký tự.")]
    public string? CitizenId { get; set; }

    [MaxLength(20, ErrorMessage = "Giới tính không hợp lệ.")]
    public string? Gender { get; set; }

    [MaxLength(100, ErrorMessage = "Quốc tịch không quá 100 ký tự.")]
    public string? Nationality { get; set; }

    [MaxLength(500, ErrorMessage = "Quê quán không quá 500 ký tự.")]
    public string? PlaceOfOrigin { get; set; }

    // ── Hôn nhân & Vợ/Chồng ───────────────────────────────────────
    /// <summary>Tình trạng hôn nhân: SINGLE, MARRIED, DIVORCED</summary>
    [MaxLength(50, ErrorMessage = "Tình trạng hôn nhân không hợp lệ.")]
    public string? MaritalStatus { get; set; }

    [MaxLength(100, ErrorMessage = "Họ tên vợ/chồng không quá 100 ký tự.")]
    public string? SpouseFullName { get; set; }

    [RegularExpression(@"^\d{9}(\d{3})?$", ErrorMessage = "Số CCCD vợ/chồng phải là 9 hoặc 12 chữ số.")]
    public string? SpouseCitizenId { get; set; }

    public DateTime? SpouseDateOfBirth { get; set; }

    [Range(0, 1_000_000_000, ErrorMessage = "Thu nhập vợ/chồng không hợp lệ.")]
    public decimal? SpouseMonthlyIncome { get; set; }

    // ── Nghề nghiệp, Nơi ở & Thu nhập ─────────────────────────────
    [MaxLength(200, ErrorMessage = "Nghề nghiệp không quá 200 ký tự.")]
    public string? Occupation { get; set; }

    [MaxLength(500, ErrorMessage = "Nơi làm việc không quá 500 ký tự.")]
    public string? WorkPlace { get; set; }

    [MaxLength(500, ErrorMessage = "Nơi ở hiện tại không quá 500 ký tự.")]
    public string? CurrentResidence { get; set; }

    [MaxLength(500, ErrorMessage = "Địa chỉ thường trú không quá 500 ký tự.")]
    public string? PermanentAddress { get; set; }

    [Range(0, 1_000_000_000, ErrorMessage = "Thu nhập hàng tháng không hợp lệ.")]
    public decimal? MonthlyIncome { get; set; }

    // ── Thực trạng nhà ở & Đối tượng ưu tiên ──────────────────────
    /// <summary>NO_HOUSE hoặc SMALL_HOUSE</summary>
    public string? HousingStatus { get; set; }

    [Range(0, 1000, ErrorMessage = "Diện tích bình quân không hợp lệ.")]
    public decimal? AverageHousingAreaPerPerson { get; set; }

    /// <summary>Nhóm đối tượng thụ hưởng theo Điều 76 Luật Nhà ở 2023</summary>
    public string? PriorityGroup { get; set; }
}
