namespace RHS.Domain.Entities;

public class PolicyConfig
{
    public Guid PolicyId { get; set; }

    public Guid UpdatedBy { get; set; }

    /// <summary>Key ổn định (xem PolicyKeys).</summary>
    public string PolicyName { get; set; } = string.Empty;

    public string PolicyValue { get; set; } = string.Empty;

    public string Category { get; set; } = "General";

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime EffectiveDate { get; set; }

    public User UpdatedByUser { get; set; } = null!;
}
