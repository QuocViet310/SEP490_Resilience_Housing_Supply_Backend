namespace RHS.Domain.Entities;

public class AIVerificationResult
{
    public Guid VerificationId { get; set; }

    public Guid DocumentId { get; set; }

    public string ExtractedText { get; set; } = string.Empty;

    public decimal FaceMatchScore { get; set; }

    public decimal RiskScore { get; set; }

    public string ValidationResult { get; set; } = string.Empty;

    public DateTime VerifiedAt { get; set; }

    // Navigation properties
    public ApplicationDocument Document { get; set; } = null!;
}
