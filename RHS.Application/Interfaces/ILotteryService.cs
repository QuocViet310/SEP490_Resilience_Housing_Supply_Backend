using RHS.Application.DTOs.Lottery;

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

    /// <summary>CĐT mở sảnh chờ → WaitingLobby</summary>
    Task<LotteryScheduleDetailDto> OpenLobbyAsync(Guid projectId, Guid actorId, CancellationToken ct = default);

    /// <summary>CĐT bắt đầu bốc → Live</summary>
    Task<LotteryScheduleDetailDto> StartLiveAsync(Guid projectId, Guid actorId, CancellationToken ct = default);

    /// <summary>CĐT kết thúc phiên → Finished + chốt người chưa bốc</summary>
    Task<LotteryScheduleDetailDto> FinishSessionAsync(Guid projectId, Guid actorId, CancellationToken ct = default);

    /// <summary>CĐT/SXD công bố → Published</summary>
    Task<LotteryScheduleDetailDto> PublishSessionAsync(Guid projectId, Guid actorId, CancellationToken ct = default);

    /// <summary>Xác thực OTP vào sảnh (Applicant). Staff luôn pass.</summary>
    Task<VerifyLotteryJoinCodeResultDto> VerifyJoinCodeAsync(
        Guid projectId, Guid userId, string? joinCode, bool isStaff, CancellationToken ct = default);
}
