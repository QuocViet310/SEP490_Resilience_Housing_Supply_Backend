using Microsoft.EntityFrameworkCore;
using RHS.Application.DTOs.HousingApplications;
using RHS.Application.DTOs.HousingApplications.Dashboard;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;
using System.Linq;

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
        // Fetch a fresh tracked entity to avoid tracking conflicts with navigation properties
        var existingApplication = await _context.HousingApplications
            .FirstOrDefaultAsync(x => x.ApplicationId == application.ApplicationId);
        
        if (existingApplication == null)
        {
            throw new InvalidOperationException($"HousingApplication with Id {application.ApplicationId} not found.");
        }

        // Update only the scalar properties that are allowed to be changed
        existingApplication.ApplicationStatus = application.ApplicationStatus;
        existingApplication.SubmittedAt = application.SubmittedAt;
        existingApplication.UpdatedAt = DateTime.UtcNow;
        existingApplication.FinalDecisionDate = application.FinalDecisionDate;
        existingApplication.OfficerId = application.OfficerId;
        existingApplication.PriorityScore = application.PriorityScore;
        existingApplication.SlotCode = application.SlotCode;
        existingApplication.ReceiptUrl = application.ReceiptUrl;

        // Form fields (applicant edit)
        existingApplication.FullName = application.FullName;
        existingApplication.CitizenId = application.CitizenId;
        existingApplication.Occupation = application.Occupation;
        existingApplication.WorkPlace = application.WorkPlace;
        existingApplication.CurrentResidence = application.CurrentResidence;
        existingApplication.PermanentAddress = application.PermanentAddress;
        existingApplication.HousingStatus = application.HousingStatus;
        existingApplication.MaritalStatus = application.MaritalStatus;
        existingApplication.HouseholdMembersCount = application.HouseholdMembersCount;
        existingApplication.PriorityGroup = application.PriorityGroup;
        existingApplication.MonthlyIncome = application.MonthlyIncome;
        existingApplication.SpouseMonthlyIncome = application.SpouseMonthlyIncome;
        existingApplication.AverageHousingAreaPerPerson = application.AverageHousingAreaPerPerson;
        existingApplication.LotteryResult = application.LotteryResult;
        existingApplication.LatestAssessmentId = application.LatestAssessmentId;
        existingApplication.IsViolation = application.IsViolation;
        existingApplication.ViolationReason = application.ViolationReason;

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
            .Include(x => x.HouseholdMembers)
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
        // Exclude REJECTED/CANCELED để cho phép người dân nộp lại hồ sơ cho cùng dự án
        return await _context.HousingApplications
            .AsNoTracking()
            .AnyAsync(x => x.ApplicantId == applicantId 
                && x.ProjectId == projectId
                && x.ApplicationStatus != ApplicationStatusConstants.Rejected
                && x.ApplicationStatus != ApplicationStatusConstants.Canceled);
    }

    public async Task<bool> HasActiveApplicationAsync(Guid applicantId)
    {
        var activeStatuses = new[]
        {
            ApplicationStatusConstants.Submitted,
            ApplicationStatusConstants.Reviewing,
            ApplicationStatusConstants.NeedMoreDocuments,
            ApplicationStatusConstants.PendingSxdReview,
            ApplicationStatusConstants.Approved,
            ApplicationStatusConstants.ApprovedByTimeout,
            ApplicationStatusConstants.DepositPaid
        };

        return await _context.HousingApplications
            .AsNoTracking()
            .AnyAsync(x => x.ApplicantId == applicantId 
                && activeStatuses.Contains(x.ApplicationStatus));
    }

    public async Task<bool> CitizenIdExistsInProjectAsync(
        string citizenId,
        Guid   projectId,
        Guid   excludeApplicationId)
    {
        // Chỉ tính hồ sơ còn hiệu lực của tài khoản Active.
        // REJECTED / CANCELED / EXPIRED không chiếm CCCD.
        // Applicant Status=Deleted (xóa mềm) không chiếm CCCD (trừ khi đã DEPOSIT_PAID — vẫn giữ để Đ38).
        var blocked = new[]
        {
            ApplicationStatusConstants.Draft,
            ApplicationStatusConstants.Submitted,
            ApplicationStatusConstants.Reviewing,
            ApplicationStatusConstants.NeedMoreDocuments,
            ApplicationStatusConstants.PendingSxdReview,
            ApplicationStatusConstants.Approved,
            ApplicationStatusConstants.ApprovedByTimeout,
            ApplicationStatusConstants.DepositPaid
        };

        return await _context.HousingApplications
            .AsNoTracking()
            .Include(x => x.Applicant)
            .AnyAsync(x =>
                x.CitizenId == citizenId
                && x.ProjectId == projectId
                && x.ApplicationId != excludeApplicationId
                && blocked.Contains(x.ApplicationStatus)
                && (
                    x.Applicant.Status == "Active"
                    || x.ApplicationStatus == ApplicationStatusConstants.DepositPaid
                ));
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
                MaritalStatus     = x.MaritalStatus,
                HouseholdMembersCount = x.HouseholdMembersCount,
                PriorityGroup     = x.PriorityGroup,
                ReceiptUrl        = x.ReceiptUrl,
                DocumentCount     = x.Documents.Count,
                IsViolation       = x.IsViolation,
                ViolationReason   = x.ViolationReason
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

    public async Task<PagedResult<HousingApplicationDashboardItemDto>> GetHousingDeveloperDashboardAsync(
        HousingApplicationDashboardQueryDto query)
    {
        var allowedStatuses = new[]
        {
            ApplicationStatusConstants.Submitted,
            ApplicationStatusConstants.Reviewing,
            ApplicationStatusConstants.NeedMoreDocuments,
            ApplicationStatusConstants.PendingSxdReview,
            ApplicationStatusConstants.Rejected
        };

        var baseQuery = _context.HousingApplications
            .AsNoTracking()
            .Include(x => x.Applicant)
            .Include(x => x.HousingProject)
            .Where(x => allowedStatuses.Contains(x.ApplicationStatus));

        // Filter theo dự án của CĐT đang đăng nhập
        if (query.DeveloperId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.HousingProject.DeveloperId == query.DeveloperId.Value);
        }

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

        // Custom sort: SUBMITTED ưu tiên nhất (chờ tiếp nhận), sau đó REVIEWING, rồi theo ngày nộp
        var sortedQuery = baseQuery
            .OrderBy(x =>
                x.ApplicationStatus == ApplicationStatusConstants.Submitted ? 0 :
                x.ApplicationStatus == ApplicationStatusConstants.Reviewing ? 1 :
                x.ApplicationStatus == ApplicationStatusConstants.NeedMoreDocuments ? 2 : 3)
            .ThenByDescending(x => x.SubmittedAt);

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
                MaritalStatus = x.MaritalStatus,
                HouseholdMembersCount = x.HouseholdMembersCount,
                PriorityGroup = x.PriorityGroup,
                ReceiptUrl = x.ReceiptUrl,
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

    public async Task<PagedResult<HousingApplicationDashboardItemDto>> GetDepartmentOfConstructionDashboardAsync(
        HousingApplicationDashboardQueryDto query)
    {
        // SXD chỉ thấy: hồ sơ đã được CĐT gửi lên (PENDING_SXD_REVIEW) + đã xử lý (APPROVED, REJECTED)
        var allowedStatuses = new[]
        {
            ApplicationStatusConstants.PendingSxdReview,
            ApplicationStatusConstants.Approved,
            ApplicationStatusConstants.ApprovedByTimeout,
            ApplicationStatusConstants.Rejected
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

        // Ưu tiên PENDING_SXD_REVIEW lên đầu (chờ hậu kiểm), sau đó theo ngày nộp
        var sortedQuery = baseQuery
            .OrderBy(x =>
                x.ApplicationStatus == ApplicationStatusConstants.PendingSxdReview ? 0 : 1)
            .ThenByDescending(x => x.SubmittedAt);

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
                MaritalStatus = x.MaritalStatus,
                HouseholdMembersCount = x.HouseholdMembersCount,
                PriorityGroup = x.PriorityGroup,
                ReceiptUrl = x.ReceiptUrl,
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

    // ─────────────────────────────────────────────────────────────
    // Batch & Final List (stubs — full implementation in later commits)
    // ─────────────────────────────────────────────────────────────

    public async Task<List<HousingApplication>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        return await _context.HousingApplications
            .Include(x => x.HousingProject)
            .Where(x => idList.Contains(x.ApplicationId))
            .ToListAsync();
    }

    public async Task<List<FinalListItemDto>> GetFinalListByProjectAsync(Guid projectId)
    {
        return await _context.HousingApplications
            .AsNoTracking()
            .Include(x => x.HousingProject)
            .Where(x => x.ProjectId == projectId
                && x.ApplicationStatus == ApplicationStatusConstants.DepositPaid)
            .Select(x => new FinalListItemDto
            {
                ApplicationId = x.ApplicationId,
                ApplicationStatus = x.ApplicationStatus,
                PriorityScore = x.PriorityScore,
                SubmittedAt = x.SubmittedAt,
                FinalDecisionDate = x.FinalDecisionDate,
                SlotCode = x.SlotCode,
                ApplicantId = x.ApplicantId,
                FullName = x.FullName,
                CitizenId = x.CitizenId,
                Occupation = x.Occupation,
                WorkPlace = x.WorkPlace,
                CurrentResidence = x.CurrentResidence,
                PermanentAddress = x.PermanentAddress,
                HousingStatus = x.HousingStatus,
                MaritalStatus = x.MaritalStatus,
                HouseholdMembersCount = x.HouseholdMembersCount,
                PriorityGroup = x.PriorityGroup,
                ProjectId = x.ProjectId,
                ProjectName = x.HousingProject.ProjectName,
                ProjectAddress = $"{x.HousingProject.Street}, {x.HousingProject.Ward}, {x.HousingProject.District}, {x.HousingProject.Province}"
            })
            .OrderBy(x => x.FullName)
            .ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Household Members — Duplicate CCCD Check
    // ─────────────────────────────────────────────────────────────

    public async Task<List<string>> FindDuplicateMemberCitizenIdsInProjectAsync(
        Guid applicationId, Guid projectId)
    {
        // Lấy tất cả CCCD thành viên hộ gia đình của hồ sơ hiện tại (bỏ qua null)
        var memberCitizenIds = await _context.HouseholdMembers
            .AsNoTracking()
            .Where(m => m.ApplicationId == applicationId && m.CitizenId != null)
            .Select(m => m.CitizenId!)
            .ToListAsync();

        if (memberCitizenIds.Count == 0)
            return new List<string>();

        // Trạng thái hồ sơ còn hiệu lực (chiếm CCCD)
        var activeStatuses = new[]
        {
            ApplicationStatusConstants.Draft,
            ApplicationStatusConstants.Submitted,
            ApplicationStatusConstants.Reviewing,
            ApplicationStatusConstants.NeedMoreDocuments,
            ApplicationStatusConstants.PendingSxdReview,
            ApplicationStatusConstants.Approved,
            ApplicationStatusConstants.ApprovedByTimeout,
            ApplicationStatusConstants.DepositPaid
        };

        // Tìm CCCD trùng: xuất hiện trong applicant CCCD hoặc member CCCD của hồ sơ KHÁC cùng project
        // Check 1: CCCD member trùng với CCCD applicant của hồ sơ khác
        var duplicateWithApplicants = await _context.HousingApplications
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId
                     && a.ApplicationId != applicationId
                     && activeStatuses.Contains(a.ApplicationStatus)
                     && memberCitizenIds.Contains(a.CitizenId))
            .Select(a => a.CitizenId)
            .Distinct()
            .ToListAsync();

        // Check 2: CCCD member trùng với CCCD member của hồ sơ khác cùng project
        var duplicateWithOtherMembers = await _context.HouseholdMembers
            .AsNoTracking()
            .Where(m => m.HousingApplication.ProjectId == projectId
                     && m.ApplicationId != applicationId
                     && m.CitizenId != null
                     && memberCitizenIds.Contains(m.CitizenId)
                     && activeStatuses.Contains(m.HousingApplication.ApplicationStatus))
            .Select(m => m.CitizenId!)
            .Distinct()
            .ToListAsync();

        return duplicateWithApplicants
            .Union(duplicateWithOtherMembers)
            .Distinct()
            .ToList();
    }
}
