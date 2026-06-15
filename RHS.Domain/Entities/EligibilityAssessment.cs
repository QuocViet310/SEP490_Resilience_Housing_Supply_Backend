namespace RHS.Domain.Entities;

public class EligibilityAssessment
{
    public Guid AssessmentId { get; set; }

    public Guid UserId { get; set; }

    public decimal EstimatedScore { get; set; }

    public bool Eligible { get; set; }

    public DateTime AssessmentDate { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
}
