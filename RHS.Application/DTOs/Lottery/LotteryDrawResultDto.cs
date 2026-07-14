namespace RHS.Application.DTOs.Lottery;

public class LotteryDrawResultDto
{
    public Guid DrawId { get; set; }
    public Guid ProjectId { get; set; }
    public DateTime DrawnAt { get; set; }
    public Guid DrawnBy { get; set; }
    public string? DrawnByName { get; set; }
    public int TotalUnits { get; set; }
    public int PriorityAllocated { get; set; }
    public int RandomAllocated { get; set; }
    public int TotalParticipants { get; set; }
    public int RandomSeed { get; set; }
    public List<LotteryParticipantResultDto> Participants { get; set; } = new();
}

public class LotteryParticipantResultDto
{
    public Guid ApplicationId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string CitizenId { get; set; } = string.Empty;
    public string? SlotCode { get; set; }
    public string? PriorityGroup { get; set; }
    public string Result { get; set; } = string.Empty;
    public bool IsPriority { get; set; }
}

public class RunLotteryRequestDto
{
    /// <summary>Số suất cần phân bổ. Null = dùng AvailableUnits còn lại của dự án (trần công bố / còn sau lần bốc trước).</summary>
    public int? TotalUnits { get; set; }
}
