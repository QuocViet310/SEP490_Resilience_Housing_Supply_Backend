using Microsoft.EntityFrameworkCore;
using RHS.Application.DTOs.Beneficiaries;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Services;

public class BeneficiaryPublishService : IBeneficiaryPublishService
{
    private readonly AppDbContext _db;

    public BeneficiaryPublishService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<BeneficiaryListItemDto>> GetPublishedBeneficiariesAsync(
        Guid? projectId = null,
        CancellationToken ct = default)
    {
        // Đ44: đối tượng đã được phân suất (trúng bốc thăm) — không gồm LOST chỉ mới đặt cọc.
        var wonResults = new[]
        {
            LotteryResultConstants.Won,
            LotteryResultConstants.PriorityWon
        };

        var query = _db.HousingApplications
            .AsNoTracking()
            .Include(a => a.HousingProject)
            .Include(a => a.PrincipleAgreement)
            .Where(a => a.ApplicationStatus == ApplicationStatusConstants.DepositPaid
                        && a.PrincipleAgreement != null
                        && a.LotteryResult != null
                        && wonResults.Contains(a.LotteryResult));

        if (projectId.HasValue)
            query = query.Where(a => a.ProjectId == projectId.Value);

        var list = await query
            .OrderByDescending(a => a.FinalDecisionDate)
            .Select(a => new BeneficiaryListItemDto
            {
                ApplicationId = a.ApplicationId,
                FullName = a.FullName,
                CitizenId = a.CitizenId,
                PermanentAddress = a.PermanentAddress,
                HouseholdMembersCount = a.HouseholdMembersCount,
                ProjectName = a.HousingProject.ProjectName,
                ProjectId = a.ProjectId,
                SlotCode = a.SlotCode,
                LotteryResult = a.LotteryResult,
                FinalDecisionDate = a.FinalDecisionDate
            })
            .ToListAsync(ct);

        return list;
    }
}
