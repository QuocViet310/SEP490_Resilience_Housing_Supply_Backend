namespace RHS.Application.DTOs.HousingProjects;

public class HousingProjectFilterRequestDto
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public string? Search { get; set; }
    public string? Province { get; set; }
    public string? District { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public double? MinArea { get; set; }
    public double? MaxArea { get; set; }
    public Guid? StatusId { get; set; }
}
