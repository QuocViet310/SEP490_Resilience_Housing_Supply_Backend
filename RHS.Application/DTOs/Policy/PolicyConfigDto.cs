namespace RHS.Application.DTOs.Policy;

public class PolicyConfigDto
{
    public Guid PolicyId { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public string PolicyValue { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime EffectiveDate { get; set; }
    public Guid UpdatedBy { get; set; }
}

public class UpdatePolicyValueRequestDto
{
    public string PolicyValue { get; set; } = string.Empty;
}
