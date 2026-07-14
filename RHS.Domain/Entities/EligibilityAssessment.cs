namespace RHS.Domain.Entities;

public class EligibilityAssessment
{
    public Guid AssessmentId { get; set; }

    public Guid UserId { get; set; }

    public Guid? ApplicationId { get; set; }

    public decimal EstimatedScore { get; set; }

    public bool Eligible { get; set; }

    /// <summary>JSON array các lý do đủ/không đủ điều kiện.</summary>
    public string? ReasonsJson { get; set; }

    public DateTime AssessmentDate { get; set; }

    public User User { get; set; } = null!;

    public HousingApplication? Application { get; set; }
}
