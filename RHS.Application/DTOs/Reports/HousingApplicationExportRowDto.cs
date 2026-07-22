using System;

namespace RHS.Application.DTOs.Reports;

/// <summary>
/// DTO dòng dữ liệu xuất báo cáo hồ sơ nhà ở xã hội.
/// </summary>
public class HousingApplicationExportRowDto
{
    public int Index { get; set; }
    public string ApplicationCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string CitizenId { get; set; } = string.Empty;
    public string DateOfBirth { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string BeneficiaryGroup { get; set; } = string.Empty;
    public string HousingStatus { get; set; } = string.Empty;
    public string ApplicationStatus { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string RejectReason { get; set; } = string.Empty;
}
