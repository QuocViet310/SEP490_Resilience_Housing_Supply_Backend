namespace RHS.Domain.Entities;

public class ApplicationStatusHistory
{
    public Guid HistoryId { get; set; }

    public Guid ApplicationId { get; set; }

    public Guid ChangedBy { get; set; }

    public string OldStatus { get; set; } = string.Empty;

    public string NewStatus { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime ChangedAt { get; set; }

    // Navigation properties
    public HousingApplication Application { get; set; } = null!;

    public User ChangedByUser { get; set; } = null!;
}
