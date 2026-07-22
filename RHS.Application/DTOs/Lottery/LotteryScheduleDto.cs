namespace RHS.Application.DTOs.Lottery;

public class CreateOrUpdateLotteryScheduleDto
{
    public DateTime LotteryDate { get; set; }

    /// <summary>Địa điểm bốc thăm trực tiếp hoặc Đường dẫn/Link phòng họp trực tuyến (Zoom, Meet...)</summary>
    public string LotteryLocation { get; set; } = string.Empty;

    /// <summary>Hình thức bốc thăm: ONLINE, OFFLINE, HYBRID</summary>
    public string LotteryType { get; set; } = "OFFLINE";

    /// <summary>Nội dung quy định tham dự, hướng dẫn hoặc ghi chú phiên bốc thăm</summary>
    public string? LotteryDescription { get; set; }

    /// <summary>Số căn hộ mở phân bổ cho phiên bốc thăm này (nếu null sẽ lấy AvailableUnits của dự án)</summary>
    public int? TotalUnits { get; set; }
}

public class LotteryScheduleDetailDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public DateTime? LotteryDate { get; set; }
    public string? LotteryLocation { get; set; }
    public string? LotteryType { get; set; }
    public string? LotteryDescription { get; set; }
    public bool? IsLotteryApproved { get; set; }
    public DateTime? LotteryApprovedAt { get; set; }
    public int AvailableUnits { get; set; }
    public int TotalEligibleParticipants { get; set; }
    public List<LotteryParticipantDto> EligibleParticipants { get; set; } = new();
}

public class LotteryParticipantDto
{
    public Guid ApplicationId { get; set; }
    public Guid ApplicantId { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public string CitizenId { get; set; } = string.Empty;
    public string? PriorityGroup { get; set; }
    public string ApplicationStatus { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}
