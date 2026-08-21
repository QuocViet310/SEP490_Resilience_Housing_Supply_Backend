namespace RHS.Application.DTOs.Lottery;

/// <summary>
/// DTO chứa toàn bộ trạng thái thời gian thực của màn hình live bốc thăm
/// (Phù hợp với giao diện Khu vực 1: Sảnh quay số & Khu vực 2: Danh sách trúng tuyển vừa bốc).
/// </summary>
public class LotteryLiveStateDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>Tên Chủ đầu tư (ví dụ: LÊ NGUYỄN GROUP)</summary>
    public string DeveloperName { get; set; } = string.Empty;

    /// <summary>Trạng thái phiên: Scheduled / WaitingLobby / Live / Paused / Finished / Published</summary>
    public string SessionStatus { get; set; } = string.Empty;

    /// <summary>Tổng số căn hộ mở phân bổ trong đợt bốc thăm (ví dụ: 100 Căn)</summary>
    public int TotalUnits { get; set; }

    /// <summary>Số lượng căn / người trúng tuyển đã bốc được (ví dụ: 71 Căn)</summary>
    public int DrawnUnitsCount { get; set; }

    /// <summary>Số lượng căn hộ còn lại chưa bốc (TotalUnits - DrawnUnitsCount)</summary>
    public int RemainingUnits { get; set; }

    /// <summary>Tổng số hồ sơ đủ điều kiện tham gia phiên bốc thăm</summary>
    public int TotalEligibleParticipants { get; set; }

    /// <summary>Số đại diện Sở Xây dựng đang online giám sát phiên</summary>
    public int SxdOnlineCount { get; set; }

    /// <summary>Số người dùng đang tham gia sảnh chờ</summary>
    public int LobbyCount { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Thống kê phiên bốc thăm (Live Statistics)
    // ─────────────────────────────────────────────────────────────

    /// <summary>Số hồ sơ trúng tuyển theo diện ưu tiên (đã bốc)</summary>
    public int PriorityWinnersCount { get; set; }

    /// <summary>Số hồ sơ trúng tuyển qua bốc thăm ngẫu nhiên (đã bốc)</summary>
    public int RandomWinnersCount { get; set; }

    /// <summary>Số lượng hồ sơ chưa thực hiện bốc thăm</summary>
    public int UndrawnParticipantsCount { get; set; }

    /// <summary>Tỷ lệ trúng tuyển dự kiến (%): TotalUnits / TotalEligibleParticipants * 100</summary>
    public double WinRatePercentage { get; set; }

    /// <summary>Ứng viên tiếp theo trong danh sách quay số (Khung vòng quay)</summary>
    public LotteryParticipantDto? NextCandidate { get; set; }

    /// <summary>Kết quả lượt vừa bốc thăm gần nhất</summary>
    public LiveDrawResultDto? LatestDrawResult { get; set; }

    /// <summary>Danh sách người trúng tuyển đã được bốc thăm trong phiên (Khu vực 2: Cập nhật tự động)</summary>
    public List<LiveDrawResultDto> RecentWinners { get; set; } = new();

    /// <summary>Khu vực 3: Thống kê quỹ căn tổng thể của dự án (ví dụ: Quỹ căn dự án CÒN LẠI: 29 / 100, 29.0%)</summary>
    public ApartmentFundQuotaStatDto ProjectApartmentFundStat { get; set; } = new();

    /// <summary>Khu vực 3: Danh sách thống kê quỹ căn (thống kê tổng thể dự án / theo từng loại căn nếu có)</summary>
    public List<ApartmentFundQuotaStatDto> ApartmentFundStats { get; set; } = new();
}

public class ApartmentFundQuotaStatDto
{
    public Guid? ApartmentTypeId { get; set; }
    public string? ApartmentTypeCode { get; set; }

    /// <summary>Tên loại căn (ví dụ: Căn 2 phòng ngủ (2PN), Căn 1 phòng ngủ (1PN))</summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Tổng số căn thuộc loại này</summary>
    public int TotalUnits { get; set; }

    /// <summary>Số căn còn lại chưa bốc</summary>
    public int RemainingUnits { get; set; }

    /// <summary>Số căn đã bốc trúng / đã phân bổ</summary>
    public int AssignedUnits { get; set; }

    /// <summary>Tỷ lệ còn lại (%): RemainingUnits / TotalUnits * 100</summary>
    public double RemainingPercentage { get; set; }
}
