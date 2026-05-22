namespace RHS.Domain.Entities;

public class HousingProjectStatus
{
    public Guid Id { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public string StatusCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public ICollection<HousingProject> HousingProjects { get; set; } = new List<HousingProject>();
}
