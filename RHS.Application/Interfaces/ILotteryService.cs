using RHS.Application.DTOs.Lottery;

namespace RHS.Application.Interfaces;

public interface ILotteryService
{
    Task<LotteryDrawResultDto> RunLotteryAsync(Guid projectId, Guid drawnBy, int? totalUnits = null, CancellationToken ct = default);
    Task<LotteryDrawResultDto?> GetLatestResultAsync(Guid projectId, CancellationToken ct = default);
}
