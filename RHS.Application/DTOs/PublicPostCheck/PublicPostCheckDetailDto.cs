namespace RHS.Application.DTOs.PublicPostCheck;

/// <summary>
/// DTO chi tiết hậu kiểm công khai của 1 hồ sơ giao dịch mua nhà ở xã hội thành công
/// </summary>
public class PublicPostCheckDetailDto
{
    public Guid ApplicationId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string MaskedCitizenId { get; set; } = string.Empty;
    public string PermanentAddress { get; set; } = string.Empty;

    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectAddress { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;

    public string? SlotCode { get; set; }
    public string? LotteryResult { get; set; }
    public int HouseholdMembersCount { get; set; }
    public string? PriorityGroup { get; set; }
    public string? PriorityGroupLabel { get; set; }

    public DateTime? FinalDecisionDate { get; set; }
    public DateTime? PrincipleAgreementCreatedAt { get; set; }
    public bool HasPrincipleAgreement { get; set; }

    public DateTime? TransferEligibleDate { get; set; }
    public bool IsUnderTransferRestriction { get; set; }
    public string RestrictionStatusText { get; set; } = string.Empty;

    /// <summary>
    /// Trích dẫn pháp lý về việc cấm chuyển nhượng NOXH
    /// </summary>
    public string LegalNote { get; set; } = "Theo Điều 62 Luật Nhà ở 2023 & Nghị định 100/2015/NĐ-CP: Bên mua nhà ở xã hội không được phép chuyển nhượng nhà ở dưới mọi hình thức trong thời hạn tối thiểu là 05 năm kể từ ngày thanh toán hết tiền mua nhà.";
}
