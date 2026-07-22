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
}
