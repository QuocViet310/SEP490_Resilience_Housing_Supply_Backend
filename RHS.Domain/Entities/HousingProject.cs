namespace RHS.Domain.Entities;

public class HousingProject
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

    public Guid HousingProjectStatusId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    // New Legal and Developer properties
    public string? DecisionNumber { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public bool IsConfirmed { get; set; }
    public DateTime? ApplicationOpenDate { get; set; }
    public DateTime? ApplicationCloseDate { get; set; }
    public string? RejectReason { get; set; }

    /// <summary>Thời điểm công bố công khai thông tin dự án (Đ38.1.b).</summary>
    public DateTime? PublicAnnounceAt { get; set; }
    
    public Guid? DeveloperId { get; set; }
    public User? Developer { get; set; }

    // Navigation Properties
    public HousingProjectStatus? HousingProjectStatus { get; set; }
    public ICollection<HousingApplication> HousingApplications { get; set; } = new List<HousingApplication>();
    public ICollection<ProjectImage> ProjectImages { get; set; } = new List<ProjectImage>();
    public ICollection<HousingQuota> HousingQuotas { get; set; } = new List<HousingQuota>();
    public ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
    public ICollection<LotteryDraw> LotteryDraws { get; set; } = new List<LotteryDraw>();
    public ICollection<ApartmentType> ApartmentTypes { get; set; } = new List<ApartmentType>();
    public ICollection<PaymentMilestone> PaymentMilestones { get; set; } = new List<PaymentMilestone>();
}
