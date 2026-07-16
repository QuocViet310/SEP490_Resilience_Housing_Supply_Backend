namespace RHS.Domain.Entities;

/// <summary>
/// Biên bản bốc thăm nhà ở xã hội (Đ38.2).
/// </summary>
public class LotteryDraw
{
    public Guid DrawId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid DrawnBy { get; set; }

    public DateTime DrawnAt { get; set; }

    public int TotalUnits { get; set; }

    public int PriorityAllocated { get; set; }

    public int RandomAllocated { get; set; }

    public int TotalParticipants { get; set; }

    /// <summary>Seed dùng để tái lập kết quả random.</summary>
    public int RandomSeed { get; set; }

    /// <summary>JSON tóm tắt kết quả (applicationId → result).</summary>
    public string ResultJson { get; set; } = "[]";

    public HousingProject HousingProject { get; set; } = null!;

    public User DrawnByUser { get; set; } = null!;
}
