using Microsoft.AspNetCore.Http;

namespace RHS.Application.DTOs.HousingProjects;

public class CreateHousingProjectRequestDto
{
    public string ProjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public double MinArea { get; set; }
    public double MaxArea { get; set; }
    public int AvailableUnits { get; set; }
    public string? ThumbnailUrl { get; set; }
    public Guid HousingProjectStatusId { get; set; }
    public List<string>? Images { get; set; }

    public IFormFile? ThumbnailFile { get; set; }
    public List<IFormFile>? ImageFiles { get; set; }
}
