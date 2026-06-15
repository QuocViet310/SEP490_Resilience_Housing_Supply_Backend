namespace RHS.Domain.Entities;

public class HousingQuota
{
    public Guid QuotaId { get; set; }

    public Guid ProjectId { get; set; }

    public string PriorityGroup { get; set; } = string.Empty;

    public int AllocatedSlots { get; set; }

    public int RemainingSlots { get; set; }

    // Navigation properties
    public HousingProject HousingProject { get; set; } = null!;
}
