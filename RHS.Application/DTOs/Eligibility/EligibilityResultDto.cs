namespace RHS.Application.DTOs.Eligibility;

public class EligibilityResultDto
{
    public Guid AssessmentId { get; set; }
    public Guid? ApplicationId { get; set; }
    public bool Eligible { get; set; }
    public decimal EstimatedScore { get; set; }
    public List<string> Reasons { get; set; } = new();
    public DateTime AssessmentDate { get; set; }
}
