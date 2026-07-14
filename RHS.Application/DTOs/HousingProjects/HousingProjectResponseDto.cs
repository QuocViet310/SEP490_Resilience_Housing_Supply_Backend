namespace RHS.Application.DTOs.HousingProjects;

public class HousingProjectResponseDto
{
    public Guid Id { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string Ward { get; set; } = string.Empty;
    public DateTime? LotteryDate { get; set; }
    public string? LotteryLocation { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public double MinArea { get; set; }
    public double MaxArea { get; set; }
    public int AvailableUnits { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? Status { get; set; }
    
    // Legal fields
    public string? DecisionNumber { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public bool IsConfirmed { get; set; }
    public DateTime? ApplicationOpenDate { get; set; }
    public DateTime? ApplicationCloseDate { get; set; }
    public string? RejectReason { get; set; }
    public DateTime? PublicAnnounceAt { get; set; }
    
    public List<ProjectImageResponseDto> Images { get; set; } = new List<ProjectImageResponseDto>();
}
