namespace RHS.Domain.Entities;

public class HousingApplication
{
    public Guid ApplicationId { get; set; }

    public Guid ApplicantId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? OfficerId { get; set; }

    public string ApplicationStatus { get; set; } = string.Empty;

    public DateTime SubmittedAt { get; set; }

    public decimal PriorityScore { get; set; }

    public decimal EstimatedMonthlyIncome { get; set; }

    public DateTime? FinalDecisionDate { get; set; }

    // Navigation properties
    public User Applicant { get; set; } = null!;

    public User? Officer { get; set; }

    public HousingProject HousingProject { get; set; } = null!;

    public ICollection<ApplicationDocument> Documents { get; set; }
        = new List<ApplicationDocument>();

    public ICollection<ApplicationStatusHistory> StatusHistories { get; set; }
        = new List<ApplicationStatusHistory>();

    public ICollection<Appointment> Appointments { get; set; }
        = new List<Appointment>();
}
