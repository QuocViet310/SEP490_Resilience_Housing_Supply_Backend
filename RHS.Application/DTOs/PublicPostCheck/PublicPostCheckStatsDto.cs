namespace RHS.Application.DTOs.PublicPostCheck;

/// <summary>
/// DTO thống kê tổng quan hậu kiểm công khai NOXH toàn quốc / địa phương
/// </summary>
public class PublicPostCheckStatsDto
{
    public int TotalAllocatedUnits { get; set; }
    public int TotalProjects { get; set; }
    public int TotalProvinces { get; set; }

    public List<ProvinceStatItemDto> ProvinceStats { get; set; } = new();
    public List<ProjectStatItemDto> ProjectStats { get; set; } = new();
}

public class ProvinceStatItemDto
{
    public string Province { get; set; } = string.Empty;
    public int TotalUnits { get; set; }
    public int TotalProjects { get; set; }
}

public class ProjectStatItemDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public int TotalUnits { get; set; }
}
