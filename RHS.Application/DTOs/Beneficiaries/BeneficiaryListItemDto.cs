namespace RHS.Application.DTOs.Beneficiaries;

/// <summary>Danh sách đối tượng đã được phân suất (trúng bốc thăm) công bố theo Đ44.</summary>
public class BeneficiaryListItemDto
{
    public Guid ApplicationId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string CitizenId { get; set; } = string.Empty;
    public string PermanentAddress { get; set; } = string.Empty;
    public int HouseholdMembersCount { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string? SlotCode { get; set; }
    public string? LotteryResult { get; set; }
    public DateTime? FinalDecisionDate { get; set; }
}
