using Microsoft.AspNetCore.Http;

namespace RHS.Application.DTOs.HousingProjects;

public class UpdateHousingProjectRequestDto
{
    public string ProjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string Ward { get; set; } = string.Empty;
    public DateTime? LotteryDate { get; set; }
    public string? LotteryLocation { get; set; }
    /// <summary>Tỉ lệ trả trước Đợt 1 (% giá căn). Bắt buộc; tối đa 30. Đợt 2 = phần còn lại.</summary>
    public decimal Phase1Percentage { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public double MinArea { get; set; }
    public double MaxArea { get; set; }
    public int AvailableUnits { get; set; }
    public string? ThumbnailUrl { get; set; }
    public Guid HousingProjectStatusId { get; set; }
    public List<string>? Images { get; set; }

    public IFormFile? ThumbnailFile { get; set; }
    public List<IFormFile>? ImageFiles { get; set; }

    // Legal properties
    public string? DecisionNumber { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public bool IsConfirmed { get; set; }
    public DateTime? ApplicationOpenDate { get; set; }
    public DateTime? ApplicationCloseDate { get; set; }

    /// <summary>Danh sách căn hộ cụ thể (tên / diện tích / giá)</summary>
    public List<DTOs.Apartment.CreateApartmentDto>? Apartments { get; set; }

    /// <summary>Cấu hình lịch thanh toán đợt (milestone templates)</summary>
    public List<DTOs.Milestone.CreateMilestoneDto>? Milestones { get; set; }
}
