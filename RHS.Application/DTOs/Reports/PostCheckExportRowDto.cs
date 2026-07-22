using System;

namespace RHS.Application.DTOs.Reports;

/// <summary>
/// DTO dòng dữ liệu xuất báo cáo hậu kiểm trùng lặp CCCD cho Sở Xây dựng.
/// </summary>
public class PostCheckExportRowDto
{
    public int Index { get; set; }
    public string CitizenId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DateOfBirth { get; set; } = string.Empty;
    public string ApplicationCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string BeneficiaryGroup { get; set; } = string.Empty;
    public string ApplicationStatus { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; }
    public bool IsDepositPaid { get; set; }
}
