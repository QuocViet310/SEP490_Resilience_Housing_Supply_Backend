using RHS.Application.DTOs.HousingApplications;
using RHS.Application.DTOs.Lottery;
using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

public interface ILotteryService
{
    Task<LotteryScheduleDetailDto> ScheduleLotteryAsync(Guid projectId, CreateOrUpdateLotteryScheduleDto dto, Guid createdBy, CancellationToken ct = default);
    Task<LotteryScheduleDetailDto> ApproveLotteryScheduleAsync(Guid projectId, Guid approvedBy, CancellationToken ct = default);
    Task<LotteryScheduleDetailDto?> GetLotteryScheduleAsync(Guid projectId, CancellationToken ct = default);
    Task<List<LotteryParticipantDto>> GetEligibleParticipantsAsync(Guid projectId, CancellationToken ct = default);
    Task<LotteryDrawResultDto> RunLotteryAsync(Guid projectId, Guid drawnBy, int? totalUnits = null, CancellationToken ct = default);
    Task<LiveDrawResultDto> DrawUnitRealtimeAsync(Guid projectId, Guid applicantId, CancellationToken ct = default);
    Task<LotteryDrawResultDto?> GetLatestResultAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>Lấy toàn bộ trạng thái thời gian thực của màn hình live bốc thăm (Tiến độ bốc, Khung quay, Kết quả vừa bốc, Danh sách trúng tuyển).</summary>
    Task<LotteryLiveStateDto> GetLiveStateAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>CĐT kích hoạt bốc 1 lượt tiếp theo ("Bốc tiếp").</summary>
    Task<LiveDrawResultDto> DrawNextTurnAsync(Guid projectId, Guid actorId, CancellationToken ct = default);

    /// <summary>CĐT mở sảnh chờ → WaitingLobby</summary>
    Task<LotteryScheduleDetailDto> OpenLobbyAsync(Guid projectId, Guid actorId, CancellationToken ct = default);

    /// <summary>CĐT bắt đầu bốc → Live</summary>
    Task<LotteryScheduleDetailDto> StartLiveAsync(Guid projectId, Guid actorId, CancellationToken ct = default);

    /// <summary>CĐT tạm dừng phiên bốc thăm → Paused</summary>
    Task<LotteryScheduleDetailDto> PauseLiveAsync(Guid projectId, Guid actorId, CancellationToken ct = default);

    /// <summary>CĐT tiếp tục phiên bốc thăm → Live</summary>
    Task<LotteryScheduleDetailDto> ResumeLiveAsync(Guid projectId, Guid actorId, CancellationToken ct = default);

    /// <summary>CĐT kết thúc phiên → Finished + chốt người chưa bốc</summary>
    Task<LotteryScheduleDetailDto> FinishSessionAsync(Guid projectId, Guid actorId, CancellationToken ct = default);

    /// <summary>SXD/Admin công bố kết quả phiên → Published (API chỉ cho SXD/Admin, không phải CĐT).</summary>
    Task<LotteryScheduleDetailDto> PublishSessionAsync(Guid projectId, Guid actorId, CancellationToken ct = default);

    /// <summary>Xác thực OTP vào sảnh (Applicant). Staff luôn pass.</summary>
    Task<VerifyLotteryJoinCodeResultDto> VerifyJoinCodeAsync(
        Guid projectId, Guid userId, string? joinCode, bool isStaff, CancellationToken ct = default);

    /// <summary>Ghi nhận SXD giám sát phiên (khi join Hub) — Đ36.2.b NĐ 100/2024.</summary>
    Task RecordSupervisorAsync(Guid projectId, Guid sxdUserId, CancellationToken ct = default);

    /// <summary>Đôn ứng viên tiếp theo trong Danh sách chờ (Waitlist) lên suất trúng mua khi có căn hộ bị trả lại / quá hạn cọc.</summary>
    Task<HousingApplication?> PromoteNextWaitlistApplicantAsync(Guid projectId, Guid? desiredApartmentTypeId, CancellationToken ct = default);

    /// <summary>Lấy Danh sách chờ (Waitlist) của dự án theo thứ tự dự bị (1, 2, 3...).</summary>
    Task<List<ApplicationSummaryResponseDto>> GetWaitlistAsync(Guid projectId, Guid? desiredApartmentTypeId = null, CancellationToken ct = default);
}
