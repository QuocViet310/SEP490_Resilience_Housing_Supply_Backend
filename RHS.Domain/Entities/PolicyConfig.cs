namespace RHS.Domain.Entities;

public class PolicyConfig
{
    public Guid PolicyId { get; set; }

    public Guid UpdatedBy { get; set; }

    public string PolicyName { get; set; } = string.Empty;

    public string PolicyValue { get; set; } = string.Empty;

    public DateTime EffectiveDate { get; set; }

    // Navigation properties
    public User UpdatedByUser { get; set; } = null!;
}
