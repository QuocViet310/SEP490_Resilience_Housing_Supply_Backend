namespace RHS.Application.DTOs.IssueReports;

public class IssueReportListItemDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string IssueType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string ReporterName { get; set; } = string.Empty;
}
