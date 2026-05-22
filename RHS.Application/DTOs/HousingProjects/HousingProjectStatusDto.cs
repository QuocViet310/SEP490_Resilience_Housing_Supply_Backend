namespace RHS.Application.DTOs.HousingProjects;

public class HousingProjectStatusDto
{
    public Guid Id { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
}
