namespace RHS.Application.DTOs.IssueReports;

public class IssueReportDetailResponseDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string IssueType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? ScreenshotUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public string ReporterName { get; set; } = string.Empty;
}
