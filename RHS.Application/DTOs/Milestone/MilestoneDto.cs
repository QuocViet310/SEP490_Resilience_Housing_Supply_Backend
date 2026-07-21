namespace RHS.Application.DTOs.Milestone;

public class MilestoneDto
{
    public Guid Id { get; set; }
    public int PhaseOrder { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public string CalculationType { get; set; } = string.Empty;
    public decimal? FixedAmount { get; set; }
    public decimal? Percentage { get; set; }
    public string TriggerEvent { get; set; } = string.Empty;
    public int DueDays { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateMilestoneDto
{
    public int PhaseOrder { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public string CalculationType { get; set; } = string.Empty;
    public decimal? FixedAmount { get; set; }
    public decimal? Percentage { get; set; }
    public string TriggerEvent { get; set; } = string.Empty;
    public int DueDays { get; set; }
    public string? Description { get; set; }
}
