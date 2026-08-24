using System.ComponentModel.DataAnnotations;
using RHS.Application.DTOs.HouseholdMember;

namespace RHS.Application.DTOs.HousingApplications;

/// <summary>
/// Request DTO để tạo mới hồ sơ đăng ký nhà ở xã hội.
/// Hỗ trợ cơ chế Auto-fill tự động trích xuất thông tin từ Hồ sơ cá nhân (Profile / eKYC / Sổ hộ khẩu / Document Vault).
/// </summary>
public class CreateApplicationRequestDto
{
    /// <summary>ID dự án nhà ở muốn đăng ký</summary>
    [Required(ErrorMessage = "ProjectId là bắt buộc.")]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Bật chế độ tự động điền từ Hồ sơ cá nhân (Profile).
    /// Nếu true: các thông tin để trống (Họ tên, CCCD, địa chỉ, tình trạng hôn nhân, thu nhập, nhân khẩu, giấy tờ)
    /// sẽ được hệ thống tự động kế thừa từ Profile đã lưu của người dùng.
    /// Mặc định: true.
    /// </summary>
    public bool AutoFillFromProfile { get; set; } = true;

    // ── Thông tin cá nhân (Tùy chọn nếu AutoFillFromProfile = true) ──

    /// <summary>Họ và tên đầy đủ</summary>
    [MaxLength(100, ErrorMessage = "Họ tên không được quá 100 ký tự.")]
    public string? FullName { get; set; }

    /// <summary>Số CCCD/CMND (9 hoặc 12 số)</summary>
    [RegularExpression(@"^\d{9}(\d{3})?$", ErrorMessage = "Số CCCD phải là 9 hoặc 12 chữ số.")]
    public string? CitizenId { get; set; }

    /// <summary>Nghề nghiệp hiện tại (không bắt buộc)</summary>
    [MaxLength(200, ErrorMessage = "Nghề nghiệp không được quá 200 ký tự.")]
    public string? Occupation { get; set; }

    /// <summary>Nơi làm việc (không bắt buộc)</summary>
    [MaxLength(500, ErrorMessage = "Nơi làm việc không được quá 500 ký tự.")]
    public string? WorkPlace { get; set; }

    // ── Thông tin địa chỉ ─────────────────────────────────────────

    /// <summary>Nơi ở hiện tại (địa chỉ thực tế đang sinh sống)</summary>
    [MaxLength(500, ErrorMessage = "Địa chỉ không được quá 500 ký tự.")]
    public string? CurrentResidence { get; set; }

    /// <summary>Nơi đăng ký thường trú/tạm trú</summary>
    [MaxLength(500, ErrorMessage = "Địa chỉ không được quá 500 ký tự.")]
    public string? PermanentAddress { get; set; }

    // ── Thực trạng nhà ở & Thu nhập ───────────────────────────────

    /// <summary>
    /// Thực trạng nhà ở. Giá trị hợp lệ:
    /// "NO_HOUSE" (Chưa có nhà) hoặc "SMALL_HOUSE" (Diện tích &lt; 10m²/người).
    /// </summary>
    public string? HousingStatus { get; set; }

    /// <summary>Tình trạng hôn nhân (SINGLE, MARRIED, DIVORCED)</summary>
    [MaxLength(50, ErrorMessage = "Tình trạng hôn nhân không được quá 50 ký tự.")]
    public string? MaritalStatus { get; set; }

    /// <summary>
    /// Danh sách thành viên hộ gia đình (không tính người đứng đơn).
    /// Nếu để trống và AutoFillFromProfile = true, hệ thống sẽ tự động lấy từ UserHouseholdMembers.
    /// </summary>
    public List<HouseholdMemberRequestDto>? HouseholdMembers { get; set; }

    /// <summary>Thuộc đối tượng thụ hưởng (Điều 76): URBAN_POOR, LOW_INCOME, INDUSTRIAL_WORKER, MERIT_PERSON...</summary>
    [MaxLength(100, ErrorMessage = "Đối tượng không được quá 100 ký tự.")]
    public string? PriorityGroup { get; set; }

    /// <summary>Thu nhập hàng tháng của người đứng đơn (VND/tháng, tối đa 15 triệu nếu độc thân).</summary>
    [Range(0, 1_000_000_000, ErrorMessage = "Thu nhập không hợp lệ.")]
    public decimal? MonthlyIncome { get; set; }

    /// <summary>Thu nhập tháng của vợ/chồng nếu đã kết hôn (VND/tháng, tổng vợ+chồng tối đa 30 triệu).</summary>
    [Range(0, 1_000_000_000, ErrorMessage = "Thu nhập vợ/chồng không hợp lệ.")]
    public decimal? SpouseMonthlyIncome { get; set; }

    /// <summary>Diện tích nhà ở bình quân đầu người (m²/người) — bắt buộc &lt; 10m² khi SMALL_HOUSE</summary>
    [Range(0, 1000, ErrorMessage = "Diện tích bình quân không hợp lệ.")]
    public decimal? AverageHousingAreaPerPerson { get; set; }

    /// <summary>Tổng diện tích nhà ở hiện có (m²) nếu muốn hệ thống tự tính diện tích bình quân đầu người</summary>
    [Range(0, 10000, ErrorMessage = "Tổng diện tích nhà không hợp lệ.")]
    public double? TotalHousingArea { get; set; }

    /// <summary>ID của Loại căn hộ mong muốn mua (ApartmentType Entity)</summary>
    public Guid? DesiredApartmentTypeId { get; set; }

    /// <summary>Mã loại căn hộ mong muốn mua: ONE_BEDROOM (1 phòng ngủ) hoặc TWO_BEDROOM (2 phòng ngủ)</summary>
    public string? DesiredApartmentType { get; set; }

    /// <summary>
    /// Tự động sao chép các tài liệu hợp lệ có sẵn trong Kho tài liệu cá nhân (Document Vault) sang hồ sơ mới.
    /// Mặc định: true.
    /// </summary>
    public bool InheritDocumentsFromVault { get; set; } = true;
}
