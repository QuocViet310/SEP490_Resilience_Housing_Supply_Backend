namespace RHS.Application.DTOs.Lottery;

public class LiveDrawResultDto
{
    public Guid ProjectId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid ApplicantId { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public string CitizenId { get; set; } = string.Empty;

    /// <summary>Kết quả: WON, PRIORITY_WON, LOST</summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>Mã căn / mã định danh căn hộ nếu trúng (ví dụ: LOT-PROJ-001)</summary>
    public string? SlotCode { get; set; }

    public DateTime DrawnAt { get; set; } = DateTime.UtcNow;

    /// <summary>Số căn hộ còn lại trong kho dự án</summary>
    public int RemainingUnits { get; set; }

    public string? PriorityGroup { get; set; }
}
