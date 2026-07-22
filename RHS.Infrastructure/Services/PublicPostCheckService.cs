using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.DTOs.PublicPostCheck;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Services;

public class PublicPostCheckService : IPublicPostCheckService
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private const string STATS_CACHE_KEY = "PublicPostCheckStats_CacheKey";

    public PublicPostCheckService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<PagedResultDto<PublicPostCheckListItemDto>> GetPublicPostCheckListAsync(
        PublicPostCheckFilterDto filter,
        CancellationToken ct = default)
    {
        var query = GetBasePublicQuery();

        // 1. Filter theo từ khóa
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(a =>
                a.FullName.ToLower().Contains(search) ||
                a.CitizenId.ToLower().Contains(search) ||
                (a.SlotCode != null && a.SlotCode.ToLower().Contains(search)) ||
                a.HousingProject.ProjectName.ToLower().Contains(search));
        }

        // 2. Filter theo số CCCD chính xác
        if (!string.IsNullOrWhiteSpace(filter.CitizenId))
        {
            var citizenId = filter.CitizenId.Trim();
            query = query.Where(a => a.CitizenId == citizenId);
        }

        // 3. Filter theo Dự án
        if (filter.ProjectId.HasValue)
        {
            query = query.Where(a => a.ProjectId == filter.ProjectId.Value);
        }

        // 4. Filter theo Tỉnh/Thành
        if (!string.IsNullOrWhiteSpace(filter.Province))
        {
            var province = filter.Province.Trim().ToLower();
            query = query.Where(a => a.HousingProject.Province.ToLower().Contains(province));
        }

        // 5. Filter theo Quận/Huyện
        if (!string.IsNullOrWhiteSpace(filter.District))
        {
            var district = filter.District.Trim().ToLower();
            query = query.Where(a => a.HousingProject.District.ToLower().Contains(district));
        }

        // 6. Filter theo Năm
        if (filter.Year.HasValue)
        {
            var year = filter.Year.Value;
            query = query.Where(a =>
                (a.FinalDecisionDate.HasValue && a.FinalDecisionDate.Value.Year == year) ||
                (!a.FinalDecisionDate.HasValue && a.SubmittedAt.Year == year));
        }

        // Phân trang chuẩn
        var totalCount = await query.CountAsync(ct);
        var pageIndex = filter.PageIndex <= 0 ? 1 : filter.PageIndex;
        var pageSize = filter.PageSize <= 0 ? 10 : (filter.PageSize > 50 ? 50 : filter.PageSize);

        var rawItems = await query
            .OrderByDescending(a => a.FinalDecisionDate ?? a.SubmittedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.ApplicationId,
                a.FullName,
                a.CitizenId,
                a.ProjectId,
                ProjectName = a.HousingProject.ProjectName,
                Province = a.HousingProject.Province,
                District = a.HousingProject.District,
                a.SlotCode,
                a.LotteryResult,
                a.HouseholdMembersCount,
                a.PriorityGroup,
                a.FinalDecisionDate,
                AgreementDate = a.PrincipleAgreement != null ? (DateTime?)a.PrincipleAgreement.CreatedAt : null
            })
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        var items = rawItems.Select(item =>
        {
            var (eligibleDate, isRestricted, statusText) = CalculateRestriction(item.FinalDecisionDate, item.AgreementDate, now);
            var groupLabel = item.PriorityGroup != null && PriorityGroupConstants.Labels.TryGetValue(item.PriorityGroup, out var l)
                ? l
                : item.PriorityGroup;

            return new PublicPostCheckListItemDto
            {
                ApplicationId = item.ApplicationId,
                FullName = item.FullName,
                MaskedCitizenId = MaskCitizenId(item.CitizenId),
                ProjectId = item.ProjectId,
                ProjectName = item.ProjectName,
                Province = item.Province,
                District = item.District,
                SlotCode = item.SlotCode,
                LotteryResult = item.LotteryResult,
                HouseholdMembersCount = item.HouseholdMembersCount,
                PriorityGroup = item.PriorityGroup,
                PriorityGroupLabel = groupLabel,
                FinalDecisionDate = item.FinalDecisionDate,
                TransferEligibleDate = eligibleDate,
                IsUnderTransferRestriction = isRestricted,
                RestrictionStatusText = statusText
            };
        }).ToList();

        return new PagedResultDto<PublicPostCheckListItemDto>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        };
    }

    public async Task<PublicPostCheckDetailDto?> GetPublicPostCheckDetailAsync(
        Guid applicationId,
        CancellationToken ct = default)
    {
        var raw = await GetBasePublicQuery()
            .Where(a => a.ApplicationId == applicationId)
            .Select(a => new
            {
                a.ApplicationId,
                a.FullName,
                a.CitizenId,
                a.PermanentAddress,
                a.ProjectId,
                ProjectName = a.HousingProject.ProjectName,
                ProjectAddress = a.HousingProject.Street + ", " + a.HousingProject.Ward + ", " + a.HousingProject.District + ", " + a.HousingProject.Province,
                Province = a.HousingProject.Province,
                District = a.HousingProject.District,
                a.SlotCode,
                a.LotteryResult,
                a.HouseholdMembersCount,
                a.PriorityGroup,
                a.FinalDecisionDate,
                HasAgreement = a.PrincipleAgreement != null,
                AgreementCreatedAt = a.PrincipleAgreement != null ? (DateTime?)a.PrincipleAgreement.CreatedAt : null
            })
            .FirstOrDefaultAsync(ct);

        if (raw == null) return null;

        var now = DateTime.UtcNow;
        var (eligibleDate, isRestricted, statusText) = CalculateRestriction(raw.FinalDecisionDate, raw.AgreementCreatedAt, now);
        var groupLabel = raw.PriorityGroup != null && PriorityGroupConstants.Labels.TryGetValue(raw.PriorityGroup, out var l)
            ? l
            : raw.PriorityGroup;

        return new PublicPostCheckDetailDto
        {
            ApplicationId = raw.ApplicationId,
            FullName = raw.FullName,
            MaskedCitizenId = MaskCitizenId(raw.CitizenId),
            PermanentAddress = raw.PermanentAddress,
            ProjectId = raw.ProjectId,
            ProjectName = raw.ProjectName,
            ProjectAddress = raw.ProjectAddress,
            Province = raw.Province,
            District = raw.District,
            SlotCode = raw.SlotCode,
            LotteryResult = raw.LotteryResult,
            HouseholdMembersCount = raw.HouseholdMembersCount,
            PriorityGroup = raw.PriorityGroup,
            PriorityGroupLabel = groupLabel,
            FinalDecisionDate = raw.FinalDecisionDate,
            HasPrincipleAgreement = raw.HasAgreement,
            PrincipleAgreementCreatedAt = raw.AgreementCreatedAt,
            TransferEligibleDate = eligibleDate,
            IsUnderTransferRestriction = isRestricted,
            RestrictionStatusText = statusText
        };
    }

    public async Task<PublicCitizenVerificationResultDto> VerifyCitizenOwnershipAsync(
        string citizenId,
        CancellationToken ct = default)
    {
        var cleanCitizenId = citizenId.Trim();
        var rawAllocations = await GetBasePublicQuery()
            .Where(a => a.CitizenId == cleanCitizenId)
            .OrderByDescending(a => a.FinalDecisionDate ?? a.SubmittedAt)
            .Select(a => new
            {
                a.ApplicationId,
                a.FullName,
                a.CitizenId,
                a.ProjectId,
                ProjectName = a.HousingProject.ProjectName,
                Province = a.HousingProject.Province,
                District = a.HousingProject.District,
                a.SlotCode,
                a.LotteryResult,
                a.HouseholdMembersCount,
                a.PriorityGroup,
                a.FinalDecisionDate,
                AgreementDate = a.PrincipleAgreement != null ? (DateTime?)a.PrincipleAgreement.CreatedAt : null
            })
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        var allocList = rawAllocations.Select(item =>
        {
            var (eligibleDate, isRestricted, statusText) = CalculateRestriction(item.FinalDecisionDate, item.AgreementDate, now);
            var groupLabel = item.PriorityGroup != null && PriorityGroupConstants.Labels.TryGetValue(item.PriorityGroup, out var l)
                ? l
                : item.PriorityGroup;

            return new PublicPostCheckListItemDto
            {
                ApplicationId = item.ApplicationId,
                FullName = item.FullName,
                MaskedCitizenId = MaskCitizenId(item.CitizenId),
                ProjectId = item.ProjectId,
                ProjectName = item.ProjectName,
                Province = item.Province,
                District = item.District,
                SlotCode = item.SlotCode,
                LotteryResult = item.LotteryResult,
                HouseholdMembersCount = item.HouseholdMembersCount,
                PriorityGroup = item.PriorityGroup,
                PriorityGroupLabel = groupLabel,
                FinalDecisionDate = item.FinalDecisionDate,
                TransferEligibleDate = eligibleDate,
                IsUnderTransferRestriction = isRestricted,
                RestrictionStatusText = statusText
            };
        }).ToList();

        if (allocList.Count == 0)
        {
            return new PublicCitizenVerificationResultDto
            {
                IsFound = false,
                SearchedCitizenId = cleanCitizenId,
                TotalHousingAllocated = 0,
                VerificationMessage = $"Chưa ghi nhận dữ liệu sở hữu hoặc giao dịch nhà ở xã hội thành công đối với CCCD '{MaskCitizenId(cleanCitizenId)}'.",
                HousingAllocations = new List<PublicPostCheckListItemDto>()
            };
        }

        var first = allocList[0];
        var msg = $"Công dân {first.FullName} (CCCD: {MaskCitizenId(cleanCitizenId)}) ĐÃ ĐƯỢC PHÂN BỔ {allocList.Count} căn nhà ở xã hội. {first.RestrictionStatusText}";

        return new PublicCitizenVerificationResultDto
        {
            IsFound = true,
            SearchedCitizenId = cleanCitizenId,
            TotalHousingAllocated = allocList.Count,
            VerificationMessage = msg,
            HousingAllocations = allocList
        };
    }

    public async Task<PublicPostCheckStatsDto> GetPublicPostCheckStatsAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(STATS_CACHE_KEY, out PublicPostCheckStatsDto? cachedStats) && cachedStats != null)
        {
            return cachedStats;
        }

        var query = GetBasePublicQuery();

        var totalAllocated = await query.CountAsync(ct);
        var totalProjects = await query.Select(a => a.ProjectId).Distinct().CountAsync(ct);
        var totalProvinces = await query.Select(a => a.HousingProject.Province).Distinct().CountAsync(ct);

        var provinceGrouped = await query
            .GroupBy(a => a.HousingProject.Province)
            .Select(g => new ProvinceStatItemDto
            {
                Province = g.Key,
                TotalUnits = g.Count(),
                TotalProjects = g.Select(x => x.ProjectId).Distinct().Count()
            })
            .OrderByDescending(p => p.TotalUnits)
            .ToListAsync(ct);

        var projectGrouped = await query
            .GroupBy(a => new { a.ProjectId, a.HousingProject.ProjectName, a.HousingProject.Province, a.HousingProject.District })
            .Select(g => new ProjectStatItemDto
            {
                ProjectId = g.Key.ProjectId,
                ProjectName = g.Key.ProjectName,
                Province = g.Key.Province,
                District = g.Key.District,
                TotalUnits = g.Count()
            })
            .OrderByDescending(p => p.TotalUnits)
            .ToListAsync(ct);

        var result = new PublicPostCheckStatsDto
        {
            TotalAllocatedUnits = totalAllocated,
            TotalProjects = totalProjects,
            TotalProvinces = totalProvinces,
            ProvinceStats = provinceGrouped,
            ProjectStats = projectGrouped
        };

        _cache.Set(STATS_CACHE_KEY, result, TimeSpan.FromMinutes(10));
        return result;
    }

    #region Helpers

    private IQueryable<Domain.Entities.HousingApplication> GetBasePublicQuery()
    {
        var wonResults = new[]
        {
            LotteryResultConstants.Won,
            LotteryResultConstants.PriorityWon
        };

        return _db.HousingApplications
            .AsNoTracking()
            .Include(a => a.HousingProject)
            .Include(a => a.PrincipleAgreement)
            .Where(a => a.ApplicationStatus == ApplicationStatusConstants.DepositPaid
                        && a.PrincipleAgreement != null
                        && a.LotteryResult != null
                        && wonResults.Contains(a.LotteryResult));
    }

    private static string MaskCitizenId(string citizenId)
    {
        if (string.IsNullOrWhiteSpace(citizenId)) return string.Empty;
        var trimmed = citizenId.Trim();
        if (trimmed.Length == 12)
        {
            // Che mờ 6 số ở giữa (chỉ giữ 4 số đầu và 2 số cuối)
            return $"{trimmed.Substring(0, 4)}******{trimmed.Substring(10, 2)}";
        }
        if (trimmed.Length <= 6) return "******";
        return $"{trimmed.Substring(0, 3)}******{trimmed.Substring(Math.Max(3, trimmed.Length - 3))}";
    }

    private static (DateTime eligibleDate, bool isRestricted, string statusText) CalculateRestriction(
        DateTime? finalDecisionDate,
        DateTime? agreementCreatedAt,
        DateTime now)
    {
        var baseDate = finalDecisionDate ?? agreementCreatedAt ?? now;
        var eligibleDate = baseDate.AddYears(5);

        if (now >= eligibleDate)
        {
            return (eligibleDate, false, "🟢 Đã đủ 5 năm - Đã được phép chuyển nhượng theo NĐ 100");
        }

        var totalMonths = ((eligibleDate.Year - now.Year) * 12) + eligibleDate.Month - now.Month;
        if (eligibleDate.Day < now.Day) totalMonths--;

        if (totalMonths < 0) totalMonths = 0;

        var years = totalMonths / 12;
        var months = totalMonths % 12;

        var timeStr = years > 0
            ? (months > 0 ? $"{years} năm {months} tháng" : $"{years} năm")
            : $"{months} tháng";

        var text = $"🔴 Cấm chuyển nhượng (Còn {timeStr})";
        return (eligibleDate, true, text);
    }

    #endregion
}
