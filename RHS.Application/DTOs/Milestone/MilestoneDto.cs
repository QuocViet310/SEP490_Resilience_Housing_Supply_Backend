using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.Milestone;

/// <summary>
/// DTO thông tin đợt thanh toán của dự án.
/// </summary>
public class MilestoneDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int PhaseOrder { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public string CalculationType { get; set; } = string.Empty;
    public decimal? FixedAmount { get; set; }
    public decimal? Percentage { get; set; }
    public string TriggerEvent { get; set; } = string.Empty;
    public string TriggerEventLabel { get; set; } = string.Empty;
    public int DueDays { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateMilestoneDto
{
    public int PhaseOrder { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public string CalculationType { get; set; } = string.Empty;
    public decimal? FixedAmount { get; set; }
    public decimal? Percentage { get; set; }
    public string TriggerEvent { get; set; } = string.Empty;
    public int DueDays { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// DTO thiết lập / cấu hình trọn gói 3 đến 6 đợt đóng tiền cho dự án.
/// </summary>
public class ConfigureProjectMilestonesRequestDto
{
    [Required(ErrorMessage = "Danh sách đợt thanh toán không được để trống.")]
    [MinLength(3, ErrorMessage = "Dự án NOXH phải có tối thiểu 3 đợt đóng tiền.")]
    [MaxLength(6, ErrorMessage = "Dự án NOXH được cấu hình tối đa 6 đợt đóng tiền.")]
    public List<MilestoneSetupItemDto> Milestones { get; set; } = new();
}

/// <summary>
/// Chi tiết từng đợt đóng tiền trong cấu hình đợt thanh toán.
/// </summary>
public class MilestoneSetupItemDto
{
    [Range(1, 6, ErrorMessage = "Thứ tự đợt thanh toán từ 1 đến 6.")]
    public int PhaseOrder { get; set; }

    [Required(ErrorMessage = "Tên đợt thanh toán không được để trống.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên đợt thanh toán từ 2 đến 100 ký tự.")]
    public string PhaseName { get; set; } = string.Empty;

    /// <summary>PERCENTAGE (mặc định) | FIXED_AMOUNT</summary>
    public string CalculationType { get; set; } = "PERCENTAGE";

    [Range(0, 100000000000, ErrorMessage = "Số tiền cố định không hợp lệ.")]
    public decimal? FixedAmount { get; set; }

    [Required(ErrorMessage = "Tỷ lệ phần trăm đợt thanh toán là bắt buộc.")]
    [Range(0.01, 100.0, ErrorMessage = "Tỷ lệ phần trăm mỗi đợt phải lớn hơn 0% và không quá 100%.")]
    public decimal? Percentage { get; set; }

    [Required(ErrorMessage = "Sự kiện kích hoạt đợt thanh toán là bắt buộc.")]
    public string TriggerEvent { get; set; } = string.Empty;

    [Range(1, 180, ErrorMessage = "Thời hạn thanh toán phải từ 1 đến 180 ngày.")]
    public int DueDays { get; set; } = 15;

    public string? Description { get; set; }
}

/// <summary>
/// Response trả về danh sách đợt thanh toán kèm tổng hợp kiểm tra % của dự án.
/// </summary>
public class ProjectMilestonesResponseDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int TotalMilestones { get; set; }
    public decimal TotalPercentage { get; set; }
    public bool IsFullyConfigured { get; set; }
    public List<MilestoneDto> Milestones { get; set; } = new();
}
