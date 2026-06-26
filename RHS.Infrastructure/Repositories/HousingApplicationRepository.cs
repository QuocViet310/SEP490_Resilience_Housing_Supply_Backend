using Microsoft.EntityFrameworkCore;
using RHS.Application.DTOs.HousingApplications;
using RHS.Application.DTOs.HousingApplications.Dashboard;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Repositories;

public class HousingApplicationRepository : IHousingApplicationRepository
{
    private readonly AppDbContext _context;

    public HousingApplicationRepository(AppDbContext context)
    {
        _context = context;
    }

    // ─────────────────────────────────────────────────────────────
    // Write
    // ─────────────────────────────────────────────────────────────

    public async Task<HousingApplication> CreateAsync(HousingApplication application)
    {
        _context.HousingApplications.Add(application);
        await _context.SaveChangesAsync();
        return application;
    }

    public async Task UpdateAsync(HousingApplication application)
    {
        application.UpdatedAt = DateTime.UtcNow;
        _context.HousingApplications.Update(application);
        await _context.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Read
    // ─────────────────────────────────────────────────────────────

    public async Task<HousingApplication?> GetByIdWithDetailsAsync(Guid applicationId)
    {
        return await _context.HousingApplications
            .AsNoTracking()
            .Include(x => x.Applicant)
            .Include(x => x.Officer)
            .Include(x => x.HousingProject)
            .Include(x => x.Documents)
                .ThenInclude(d => d.UploadedByUser)
            .Include(x => x.Documents)
                .ThenInclude(d => d.VerificationResult)
            .Include(x => x.StatusHistories.OrderByDescending(h => h.ChangedAt))
                .ThenInclude(h => h.ChangedByUser)
            .FirstOrDefaultAsync(x => x.ApplicationId == applicationId);
    }

    public async Task<PagedResultDto<ApplicationSummaryResponseDto>> GetByApplicantAsync(
        Guid applicantId,
        ApplicationFilterRequestDto filter)
    {
        var query = BuildBaseQuery()
            .Where(x => x.ApplicantId == applicantId);

        query = ApplyFilters(query, filter);

        return await ExecutePagedQueryAsync(query, filter);
    }

    public async Task<PagedResultDto<ApplicationSummaryResponseDto>> GetAllAsync(
        ApplicationFilterRequestDto filter)
    {
        var query = BuildBaseQuery();
        query = ApplyFilters(query, filter);
        return await ExecutePagedQueryAsync(query, filter);
    }

    // ─────────────────────────────────────────────────────────────
    // Check
    // ─────────────────────────────────────────────────────────────

    public async Task<bool> ExistsByApplicantAndProjectAsync(Guid applicantId, Guid projectId)
    {
        return await _context.HousingApplications
            .AsNoTracking()
            .AnyAsync(x => x.ApplicantId == applicantId && x.ProjectId == projectId);
    }

    public async Task<bool> CitizenIdExistsInProjectAsync(
        string citizenId,
        Guid   projectId,
        Guid   excludeApplicationId)
    {
        return await _context.HousingApplications
            .AsNoTracking()
            .AnyAsync(x =>
                x.CitizenId    == citizenId         &&
                x.ProjectId    == projectId          &&
                x.ApplicationId != excludeApplicationId);
    }

    // ─────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Query cơ sở cho danh sách hồ sơ (không lọc theo Applicant).
    /// Include Project và Applicant để hiển thị thông tin tóm tắt.
    /// </summary>
    private IQueryable<HousingApplication> BuildBaseQuery()
    {
        return _context.HousingApplications
            .AsNoTracking()
            .Include(x => x.Applicant)
            .Include(x => x.HousingProject);
    }

    /// <summary>Áp dụng các điều kiện lọc vào query.</summary>
    private static IQueryable<HousingApplication> ApplyFilters(
        IQueryable<HousingApplication> query,
        ApplicationFilterRequestDto filter)
    {
        // Lọc theo trạng thái
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(x => x.ApplicationStatus == filter.Status);

        // Lọc theo dự án
        if (filter.ProjectId.HasValue)
            query = query.Where(x => x.ProjectId == filter.ProjectId.Value);

        // Tìm kiếm theo Họ tên hoặc CCCD
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(x =>
                x.FullName.ToLower().Contains(term) ||
                x.CitizenId.Contains(term));
        }

        // Lọc theo ngày nộp
        if (filter.SubmittedFrom.HasValue)
            query = query.Where(x => x.SubmittedAt >= filter.SubmittedFrom.Value);

        if (filter.SubmittedTo.HasValue)
            query = query.Where(x => x.SubmittedAt <= filter.SubmittedTo.Value);

        return query;
    }

    /// <summary>
    /// Thực thi query với phân trang và map kết quả sang DTO tóm tắt.
    /// </summary>
    private static async Task<PagedResultDto<ApplicationSummaryResponseDto>> ExecutePagedQueryAsync(
        IQueryable<HousingApplication> query,
        ApplicationFilterRequestDto filter)
    {
        // Đếm tổng trước khi phân trang
        var totalCount = await query.CountAsync();

        // Sắp xếp mới nhất lên đầu
        query = query.OrderByDescending(x => x.CreatedAt);

        // Phân trang
        var pageIndex = Math.Max(filter.PageIndex, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, 50);
        var skip = (pageIndex - 1) * pageSize;

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new ApplicationSummaryResponseDto
            {
                ApplicationId     = x.ApplicationId,
                ProjectId         = x.ProjectId,
                ProjectName       = x.HousingProject.ProjectName,
                ApplicantId       = x.ApplicantId,
                ApplicantFullName = x.FullName,
                CitizenId         = x.CitizenId,
                ApplicationStatus = x.ApplicationStatus,
                CreatedAt         = x.CreatedAt,
                SubmittedAt       = x.SubmittedAt,
                FinalDecisionDate = x.FinalDecisionDate,
                HousingStatus     = x.HousingStatus,
                EstimatedMonthlyIncome = x.EstimatedMonthlyIncome,
                DocumentCount     = x.Documents.Count
            })
            .ToListAsync();

        return new PagedResultDto<ApplicationSummaryResponseDto>
        {
            PageIndex  = pageIndex,
            PageSize   = pageSize,
            TotalCount = totalCount,
            Items      = items
        };
    }

    public async Task<PagedResult<HousingApplicationDashboardItemDto>> GetVerificationOfficerDashboardAsync(
        HousingApplicationDashboardQueryDto query)
    {
        var allowedStatuses = new[]
        {
            ApplicationStatusConstants.Submitted,
            ApplicationStatusConstants.UnderReview,
            ApplicationStatusConstants.NeedMoreDocuments
        };

        var baseQuery = _context.HousingApplications
            .AsNoTracking()
            .Include(x => x.Applicant)
            .Include(x => x.HousingProject)
            .Where(x => allowedStatuses.Contains(x.ApplicationStatus));

        if (query.ProjectId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.ProjectId == query.ProjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var statusUpper = query.Status.Trim().ToUpper();
            if (allowedStatuses.Contains(statusUpper))
            {
                baseQuery = baseQuery.Where(x => x.ApplicationStatus == statusUpper);
            }
            else
            {
                baseQuery = baseQuery.Where(x => false);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(x =>
                x.FullName.ToLower().Contains(term) ||
                x.Applicant.Email.ToLower().Contains(term) ||
                x.HousingProject.ProjectName.ToLower().Contains(term));
        }

        var totalCount = await baseQuery.CountAsync();

        var sortedQuery = baseQuery.OrderByDescending(x => x.SubmittedAt);

        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);
        var skip = (pageIndex - 1) * pageSize;

        var items = await sortedQuery
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new HousingApplicationDashboardItemDto
            {
                ApplicationId = x.ApplicationId,
                ApplicantName = x.FullName,
                ApplicantEmail = x.Applicant.Email,
                ProjectName = x.HousingProject.ProjectName,
                ApplicationStatus = x.ApplicationStatus,
                PriorityScore = x.PriorityScore,
                EstimatedMonthlyIncome = x.EstimatedMonthlyIncome,
                SubmittedAt = x.SubmittedAt
            })
            .ToListAsync();

        return new PagedResult<HousingApplicationDashboardItemDto>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        };
    }

    public async Task<PagedResult<HousingApplicationDashboardItemDto>> GetWardManagerDashboardAsync(
        HousingApplicationDashboardQueryDto query)
    {
        var baseQuery = _context.HousingApplications
            .AsNoTracking()
            .Include(x => x.Applicant)
            .Include(x => x.HousingProject)
            .Where(x => x.ApplicationStatus == ApplicationStatusConstants.UnderReview);

        if (query.ProjectId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.ProjectId == query.ProjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(x =>
                x.FullName.ToLower().Contains(term) ||
                x.Applicant.Email.ToLower().Contains(term) ||
                x.HousingProject.ProjectName.ToLower().Contains(term));
        }

        var totalCount = await baseQuery.CountAsync();

        var sortedQuery = baseQuery.OrderByDescending(x => x.SubmittedAt);

        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);
        var skip = (pageIndex - 1) * pageSize;

        var items = await sortedQuery
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new HousingApplicationDashboardItemDto
            {
                ApplicationId = x.ApplicationId,
                ApplicantName = x.FullName,
                ApplicantEmail = x.Applicant.Email,
                ProjectName = x.HousingProject.ProjectName,
                ApplicationStatus = x.ApplicationStatus,
                PriorityScore = x.PriorityScore,
                EstimatedMonthlyIncome = x.EstimatedMonthlyIncome,
                SubmittedAt = x.SubmittedAt
            })
            .ToListAsync();

        return new PagedResult<HousingApplicationDashboardItemDto>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        };
    }
}
