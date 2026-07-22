using System;

namespace RHS.Application.DTOs.Reports;

/// <summary>
/// DTO dòng dữ liệu xuất báo cáo kết quả bốc thăm / phân suất căn hộ (Điều 44).
/// </summary>
public class LotteryResultExportRowDto
{
    public int Index { get; set; }
    public string ApplicationCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string CitizenId { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string BeneficiaryGroup { get; set; } = string.Empty;
    public string LotteryResult { get; set; } = string.Empty; // PRIORITY_WON, WON, LOST
    public string SlotCode { get; set; } = string.Empty;
    public DateTime? DrawnAt { get; set; }
    public bool HasPrincipleAgreement { get; set; }
}
