namespace RHS.Domain.Entities;

public class ProjectImage
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public HousingProject HousingProject { get; set; } = null!;
}
