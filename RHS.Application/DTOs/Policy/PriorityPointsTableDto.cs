using System.Collections.Generic;

namespace RHS.Application.DTOs.Policy;

public class PriorityGroupPointItemDto
{
    public string GroupCode { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public int Points { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class PriorityPointsTableDto
{
    public List<PriorityGroupPointItemDto> PointsTable { get; set; } = new();
}
