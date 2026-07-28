using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHS.Domain.Constants;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Helpers;

/// <summary>
/// Số căn trống / suất bốc thăm lấy từ bảng Apartments (AVAILABLE),
/// trừ các hồ sơ đang giữ suất (CONTRACT_PENDING / CONTRACT_SIGNED) chưa gán căn cụ thể.
/// </summary>
public static class ProjectUnitSeatHelper
{
    public static bool HoldsReservedUnit(string? applicationStatus) =>
        applicationStatus is
            ApplicationStatusConstants.ContractPending or
            ApplicationStatusConstants.ContractSigned;

    /// <summary>
    /// Đồng bộ AvailableUnits = Count(AVAILABLE) − soft-hold (CONTRACT_* chưa có ApartmentId).
    /// Đây là căn cứ chốt danh sách / bốc thăm khi dự án đã có dòng căn.
    /// Dùng ToList + đếm in-memory để phản ánh thay đổi đang track chưa SaveChanges.
    /// </summary>
    public static async Task<int> SyncAvailableUnitsAsync(
        AppDbContext db,
        Guid projectId,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        var project = await db.HousingProjects
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);
        if (project == null)
            return 0;

        var apartments = await db.Apartments
            .Where(a => a.ProjectId == projectId)
            .ToListAsync(ct);

        if (apartments.Count == 0)
        {
            // Legacy: chưa có bảng căn — giữ nguyên counter
            return project.AvailableUnits;
        }

        var availableApts = apartments.Count(a =>
            string.Equals(a.Status, ApartmentStatusConstants.Available, StringComparison.OrdinalIgnoreCase));

        // Hồ sơ đã chốt/trúng nhưng chưa gắn căn cụ thể vẫn giữ 1 suất
        var holdingApps = await db.HousingApplications
            .Where(a => a.ProjectId == projectId
                        && (a.ApplicationStatus == ApplicationStatusConstants.ContractPending
                            || a.ApplicationStatus == ApplicationStatusConstants.ContractSigned))
            .ToListAsync(ct);

        var softHolds = holdingApps.Count(a => a.ApartmentId == null);

        var effective = Math.Max(0, availableApts - softHolds);
        if (project.AvailableUnits != effective)
        {
            logger?.LogInformation(
                "Sync AvailableUnits project {ProjectId}: {Old} → {New} (AVAILABLE={Avail}, softHolds={Holds}).",
                projectId, project.AvailableUnits, effective, availableApts, softHolds);
            project.AvailableUnits = effective;
            project.UpdatedAt = DateTime.UtcNow;
        }

        return effective;
    }

    /// <summary>
    /// Hoàn căn đã cấp (nếu có) về AVAILABLE rồi sync lại AvailableUnits.
    /// </summary>
    public static async Task<bool> TryReleaseReservedUnitAsync(
        AppDbContext db,
        Guid projectId,
        string? previousStatus,
        Guid? apartmentId = null,
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

        string? releasedUnitName = null;
        if (apartmentId.HasValue)
        {
            var apt = await db.Apartments
                .FirstOrDefaultAsync(a => a.Id == apartmentId.Value && a.ProjectId == projectId, ct);
            if (apt != null
                && string.Equals(apt.Status, ApartmentStatusConstants.Assigned, StringComparison.OrdinalIgnoreCase))
            {
                apt.Status = ApartmentStatusConstants.Available;
                releasedUnitName = apt.UnitName;
            }
        }

        var apartmentRows = await db.Apartments.AnyAsync(a => a.ProjectId == projectId, ct);
        if (apartmentRows)
        {
            await SyncAvailableUnitsAsync(db, projectId, logger, ct);
        }
        else
        {
            project.AvailableUnits += 1;
            project.UpdatedAt = DateTime.UtcNow;
        }

        logger?.LogInformation(
            "Released reserved unit for project {ProjectId}. Unit={Unit}, AvailableUnits={Units} (from app status {Status}).",
            projectId, releasedUnitName ?? "(soft-hold)", project.AvailableUnits, previousStatus);

        return true;
    }
}
