using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RHS.Application.DTOs.Admin;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Services;

public class SuperAdminDashboardService : ISuperAdminDashboardService
{
    private readonly AppDbContext _db;

    public SuperAdminDashboardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PlatformOverviewStatDto> GetOverviewStatsAsync(CancellationToken ct = default)
    {
        int totalProjects = await _db.HousingProjects.CountAsync(p => !p.IsDeleted, ct);
        int totalHousingDevelopers = await _db.Users.CountAsync(u => u.Role.RoleName == RoleConstants.HousingDeveloper, ct);
        int totalSxdOfficers = await _db.Users.CountAsync(u => u.Role.RoleName == RoleConstants.DepartmentOfConstruction, ct);
        int totalApplicants = await _db.Users.CountAsync(u => u.Role.RoleName == RoleConstants.Applicant, ct);
        int totalApplications = await _db.HousingApplications.CountAsync(ct);
        int totalApartments = await _db.Apartments.CountAsync(ct);

        // Calculate total contract value (from HousingApplications with linked apartments)
        var totalContractValueVnd = await _db.HousingApplications
            .Where(a => a.ApartmentId != null && a.Apartment != null)
            .SumAsync(a => a.Apartment!.Price, ct);

        // Calculate total payments collected (Success status)
        var totalCollectedPaymentVnd = await _db.Payments
            .Where(p => p.Status == "Success" || p.Status == "COMPLETED" || p.Status == "Completed")
            .SumAsync(p => p.Amount, ct);

        return new PlatformOverviewStatDto
        {
            TotalProjects = totalProjects,
            TotalHousingDevelopers = totalHousingDevelopers,
            TotalSxdOfficers = totalSxdOfficers,
            TotalApplicants = totalApplicants,
            TotalApplications = totalApplications,
            TotalApartments = totalApartments,
            TotalContractValueVnd = totalContractValueVnd,
            TotalCollectedPaymentVnd = totalCollectedPaymentVnd,
            LastUpdatedAt = DateTime.UtcNow
        };
    }

    public async Task<List<PlatformAbsorptionStatDto>> GetAbsorptionStatsAsync(CancellationToken ct = default)
    {
        var projects = await _db.HousingProjects
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .Include(p => p.Apartments)
            .ToListAsync(ct);

        var list = new List<PlatformAbsorptionStatDto>();

        foreach (var p in projects)
        {
            int totalUnits = p.Apartments.Count > 0 ? p.Apartments.Count : p.AvailableUnits;
            int depositedOrSoldUnits = p.Apartments.Count(a => a.Status != ApartmentStatusConstants.Available);
            int availableUnits = p.Apartments.Count > 0
                ? p.Apartments.Count(a => a.Status == ApartmentStatusConstants.Available)
                : p.AvailableUnits;

            double rate = totalUnits > 0
                ? Math.Min(100.0, Math.Round((double)depositedOrSoldUnits / totalUnits * 100.0, 1))
                : 0.0;

            list.Add(new PlatformAbsorptionStatDto
            {
                ProjectId = p.Id,
                ProjectName = p.ProjectName,
                Province = p.Province,
                TotalUnits = totalUnits,
                DepositedOrSoldUnits = depositedOrSoldUnits,
                AvailableUnits = availableUnits,
                AbsorptionRatePercentage = rate
            });
        }

        return list.OrderByDescending(x => x.AbsorptionRatePercentage).ToList();
    }

    public async Task<PlatformDisbursementStatDto> GetDisbursementStatsAsync(CancellationToken ct = default)
    {
        var installments = await _db.PaymentInstallments
            .AsNoTracking()
            .ToListAsync(ct);

        decimal totalDue = installments.Sum(i => i.Amount);
        decimal totalPaid = installments.Where(i => i.Status == InstallmentStatusConstants.Paid).Sum(i => i.Amount);

        var now = DateTime.UtcNow;
        var overdueInstallments = installments
            .Where(i => i.Status != InstallmentStatusConstants.Paid && i.DueDate < now)
            .ToList();

        decimal totalOverdue = overdueInstallments.Sum(i => i.Amount);

        // Tính tổng tiền lãi phạt tích lũy (0.05%/ngày)
        decimal totalPenaltyAccrued = overdueInstallments.Sum(i =>
        {
            int overdueDays = (now - i.DueDate).Days;
            return overdueDays > 0 ? (decimal)overdueDays * i.Amount * 0.0005m : 0m;
        });

        double collectionRate = totalDue > 0
            ? Math.Min(100.0, Math.Round((double)totalPaid / (double)totalDue * 100.0, 1))
            : 0.0;

        return new PlatformDisbursementStatDto
        {
            TotalDueInstallmentAmountVnd = totalDue,
            TotalPaidInstallmentAmountVnd = totalPaid,
            TotalOverdueAmountVnd = totalOverdue,
            TotalPenaltyInterestAccruedVnd = totalPenaltyAccrued,
            PaymentCollectionRatePercentage = collectionRate
        };
    }

    public async Task<PlatformApplicationRatioDto> GetApplicationValidityRatiosAsync(CancellationToken ct = default)
    {
        var apps = await _db.HousingApplications
            .AsNoTracking()
            .Select(a => new { a.ApplicationStatus, a.IsViolation })
            .ToListAsync(ct);

        int totalCount = apps.Count;
        int approvedCount = apps.Count(a => a.ApplicationStatus == ApplicationStatusConstants.Approved);
        int approvedByTimeoutCount = apps.Count(a => a.ApplicationStatus == ApplicationStatusConstants.ApprovedByTimeout);
        int rejectedCount = apps.Count(a => a.ApplicationStatus == ApplicationStatusConstants.Rejected);
        int pendingCount = apps.Count(a => a.ApplicationStatus == ApplicationStatusConstants.Submitted
                                         || a.ApplicationStatus == ApplicationStatusConstants.Reviewing
                                         || a.ApplicationStatus == ApplicationStatusConstants.PendingSxdReview
                                         || a.ApplicationStatus == ApplicationStatusConstants.NeedMoreDocuments);
        int violationCount = apps.Count(a => a.IsViolation);
        int cancelledCount = apps.Count(a => a.ApplicationStatus == ApplicationStatusConstants.Canceled || a.ApplicationStatus == ApplicationStatusConstants.CancellationRequested);

        int validTotal = approvedCount + approvedByTimeoutCount;
        double validityPct = totalCount > 0 ? Math.Round((double)validTotal / totalCount * 100.0, 1) : 0.0;
        double rejectionPct = totalCount > 0 ? Math.Round((double)rejectedCount / totalCount * 100.0, 1) : 0.0;
        double violationPct = totalCount > 0 ? Math.Round((double)violationCount / totalCount * 100.0, 1) : 0.0;

        return new PlatformApplicationRatioDto
        {
            TotalApplications = totalCount,
            ApprovedCount = approvedCount,
            ApprovedByTimeoutCount = approvedByTimeoutCount,
            RejectedCount = rejectedCount,
            PendingCount = pendingCount,
            ViolationCount = violationCount,
            CancelledCount = cancelledCount,
            ValidityPercentage = validityPct,
            RejectionPercentage = rejectionPct,
            ViolationPercentage = violationPct
        };
    }
}
