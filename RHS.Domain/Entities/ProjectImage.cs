namespace RHS.Domain.Entities;

public class ProjectImage
{
    public Guid ImageId { get; set; }

    public Guid ProjectId { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    // Navigation properties
    public HousingProject HousingProject { get; set; } = null!;
}
