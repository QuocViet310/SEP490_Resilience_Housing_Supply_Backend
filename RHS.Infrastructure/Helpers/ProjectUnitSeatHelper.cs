using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHS.Domain.Constants;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Helpers;

/// <summary>
/// Suất căn được giữ khi hồ sơ vào CONTRACT_PENDING (chốt / ưu tiên / trúng thăm)
/// và vẫn giữ qua CONTRACT_SIGNED đến khi đặt cọc. Hết hạn / huỷ ở các trạng thái này phải hoàn suất.
/// </summary>
public static class ProjectUnitSeatHelper
{
    public static bool HoldsReservedUnit(string? applicationStatus) =>
        applicationStatus is
            ApplicationStatusConstants.ContractPending or
            ApplicationStatusConstants.ContractSigned;

    /// <summary>
    /// Hoàn 1 suất vào AvailableUnits nếu status trước đó đang giữ suất.
    /// Idempotent theo lần gọi: chỉ gọi một lần khi chuyển sang EXPIRED/CANCELED.
    /// </summary>
    public static async Task<bool> TryReleaseReservedUnitAsync(
        AppDbContext db,
        Guid projectId,
        string? previousStatus,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        if (!HoldsReservedUnit(previousStatus))
            return false;

        var project = await db.HousingProjects
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);

        if (project == null)
        {
            logger?.LogWarning(
                "Cannot release unit seat: project {ProjectId} not found (previousStatus={Status}).",
                projectId, previousStatus);
            return false;
        }

        project.AvailableUnits += 1;
        project.UpdatedAt = DateTime.UtcNow;

        logger?.LogInformation(
            "Released 1 reserved unit for project {ProjectId}. AvailableUnits={Units} (from app status {Status}).",
            projectId, project.AvailableUnits, previousStatus);

        return true;
    }
}
