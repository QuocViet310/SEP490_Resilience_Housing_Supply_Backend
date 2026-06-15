namespace RHS.Application.DTOs.IssueReports;

public class CreateIssueReportRequestDto
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string IssueType { get; set; } = string.Empty;

    public string? ScreenshotUrl { get; set; }
}
