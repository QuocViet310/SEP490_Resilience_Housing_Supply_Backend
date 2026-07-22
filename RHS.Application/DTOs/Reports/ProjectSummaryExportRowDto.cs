using System;

namespace RHS.Application.DTOs.Reports;

/// <summary>
/// DTO dòng dữ liệu xuất báo cáo tổng hợp tiến độ các dự án NOXH.
/// </summary>
public class ProjectSummaryExportRowDto
{
    public int Index { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string DeveloperName { get; set; } = string.Empty;
    public string DecisionNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int TotalUnits { get; set; }
    public int AvailableUnits { get; set; }
    public int TotalApplications { get; set; }
    public int ApprovedApplications { get; set; }
    public int DepositPaidApplications { get; set; }
    public string ProjectStatus { get; set; } = string.Empty;
    public string ApplicationOpenDate { get; set; } = string.Empty;
    public string ApplicationCloseDate { get; set; } = string.Empty;
}
