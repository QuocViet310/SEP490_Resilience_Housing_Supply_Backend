namespace RHS.Application.DTOs.HousingProjects;

public class ProjectImageResponseDto
{
    public Guid Id { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
}
