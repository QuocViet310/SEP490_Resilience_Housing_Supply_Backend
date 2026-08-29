using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RHS.Application.DTOs.Admin;

namespace RHS.Application.Interfaces;

public interface ISuperAdminDashboardService
{
    Task<PlatformOverviewStatDto> GetOverviewStatsAsync(CancellationToken ct = default);
    Task<List<PlatformAbsorptionStatDto>> GetAbsorptionStatsAsync(CancellationToken ct = default);
    Task<PlatformDisbursementStatDto> GetDisbursementStatsAsync(CancellationToken ct = default);
    Task<PlatformApplicationRatioDto> GetApplicationValidityRatiosAsync(CancellationToken ct = default);
}
