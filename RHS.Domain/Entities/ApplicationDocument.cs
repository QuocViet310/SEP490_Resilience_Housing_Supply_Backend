namespace RHS.Domain.Entities;

public class ApplicationDocument
{
    public Guid DocumentId { get; set; }

    public Guid ApplicationId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string FileUrl { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; }

    public string VerificationStatus { get; set; } = string.Empty;

    // Navigation properties
    public HousingApplication HousingApplication { get; set; } = null!;

    public AIVerificationResult? VerificationResult { get; set; }
}
