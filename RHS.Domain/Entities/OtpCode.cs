namespace RHS.Domain.Entities;

public class OtpCode
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty; // Registration, PasswordReset, etc.
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }

    // Navigation property
    public User User { get; set; } = null!;
}
