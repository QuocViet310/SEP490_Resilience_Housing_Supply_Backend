namespace RHS.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? PasswordHash { get; set; }
    public string? CitizenId { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = "Active"; // Active, Inactive, Suspended
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Legacy fields for backward compatibility (will be removed later)
    public bool IsEmailVerified { get; set; }
    public string? GoogleId { get; set; }
    public string? ProfileImageUrl { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Navigation properties
    public Role Role { get; set; } = null!;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<OtpVerification> OtpVerifications { get; set; } = new List<OtpVerification>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    // New Navigation Properties
    public ICollection<HousingApplication> HousingApplications { get; set; } = new List<HousingApplication>();
    public ICollection<HousingApplication> AssignedApplications { get; set; } = new List<HousingApplication>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<EligibilityAssessment> EligibilityAssessments { get; set; } = new List<EligibilityAssessment>();
    public ICollection<Message> SentMessages { get; set; } = new List<Message>();
    public ICollection<Message> ReceivedMessages { get; set; } = new List<Message>();
}
