using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.HouseholdMember;
using RHS.Application.DTOs.HousingApplications;
using RHS.Application.DTOs.HousingApplications.Dashboard;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;
using RHS.Infrastructure.Exceptions;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Service xử lý nghiệp vụ tạo hồ sơ và xem hồ sơ nhà ở xã hội.
/// </summary>
public class HousingApplicationService : IHousingApplicationService
{
    private readonly IHousingApplicationRepository _applicationRepo;
    private readonly IEligibilityRuleEngine _eligibilityEngine;
    private readonly AppDbContext _context;
    private readonly ILogger<HousingApplicationService> _logger;

    public HousingApplicationService(
        IHousingApplicationRepository applicationRepo,
        IEligibilityRuleEngine eligibilityEngine,
        AppDbContext context,
        ILogger<HousingApplicationService> logger)
    {
        _applicationRepo = applicationRepo;
        _eligibilityEngine = eligibilityEngine;
        _context = context;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────
    // Tạo hồ sơ
    // ─────────────────────────────────────────────────────────────

    public async Task<CreateApplicationResponseDto> CreateApplicationAsync(
        Guid applicantId,
        CreateApplicationRequestDto request)
    {
        _logger.LogInformation(
            "Applicant {ApplicantId} đang tạo hồ sơ cho dự án {ProjectId}.",
            applicantId, request.ProjectId);

        // 1. Validate: HousingStatus phải hợp lệ
        if (!HousingStatusConstants.IsValid(request.HousingStatus))
        {
            throw new ArgumentException(
                $"Thực trạng nhà ở '{request.HousingStatus}' không hợp lệ. " +
                $"Giá trị cho phép: {string.Join(", ", HousingStatusConstants.AllValues)}");
        }

        if (!PriorityGroupConstants.IsValid(request.PriorityGroup))
        {
            throw new ArgumentException(
                "Đối tượng phải là hộ nghèo đô thị (URBAN_POOR) hoặc hộ cận nghèo đô thị (URBAN_NEAR_POOR).");
        }

        // 2. Kiểm tra trùng lặp: mỗi Applicant chỉ được 1 hồ sơ/dự án
        var alreadyExists = await _applicationRepo.ExistsByApplicantAndProjectAsync(
            applicantId, request.ProjectId);

        if (alreadyExists)
        {
            _logger.LogWarning(
                "Applicant {ApplicantId} đã có hồ sơ cho dự án {ProjectId}.",
                applicantId, request.ProjectId);
            throw new DuplicateApplicationException(applicantId, request.ProjectId);
        }

        // 2b. Active App Check: chống nộp nhiều nơi
        var hasActiveApplication = await _applicationRepo.HasActiveApplicationAsync(applicantId);
        if (hasActiveApplication)
        {
            _logger.LogWarning(
                "Applicant {ApplicantId} đã có hồ sơ đang hoạt động ở một dự án khác. Không thể tạo đơn nháp mới.",
                applicantId);
            throw new ActiveApplicationExistsException(applicantId);
        }

        // 3. Map DTO → Entity (trạng thái ban đầu = DRAFT)
        var now = DateTime.UtcNow;
        var application = new HousingApplication
        {
            ApplicationId          = Guid.NewGuid(),
            ApplicantId            = applicantId,
            ProjectId              = request.ProjectId,
            ApplicationStatus      = ApplicationStatusConstants.Draft,
            CreatedAt              = now,
            SubmittedAt            = now,       // sẽ được cập nhật khi SUBMIT thực sự
            PriorityScore          = 0,
            FinalDecisionDate      = null,
            // Form fields
            FullName               = request.FullName.Trim(),
            CitizenId              = request.CitizenId.Trim(),
            Occupation             = request.Occupation?.Trim(),
            WorkPlace              = request.WorkPlace?.Trim(),
            CurrentResidence       = request.CurrentResidence.Trim(),
            PermanentAddress       = request.PermanentAddress.Trim(),
            HousingStatus          = request.HousingStatus,
            MaritalStatus          = request.MaritalStatus.Trim(),
            HouseholdMembersCount  = 1 + (request.HouseholdMembers?.Count ?? 0),
            PriorityGroup          = request.PriorityGroup.Trim(),
            MonthlyIncome          = request.MonthlyIncome,
            SpouseMonthlyIncome    = request.SpouseMonthlyIncome,
            AverageHousingAreaPerPerson = request.AverageHousingAreaPerPerson,
            LotteryResult          = LotteryResultConstants.Pending,
            HouseholdMembers       = MapHouseholdMembers(request.HouseholdMembers)
        };

        // 4. Lưu vào DB
        await _applicationRepo.CreateAsync(application);

        _logger.LogInformation(
            "Tạo hồ sơ thành công. ApplicationId={ApplicationId}, Status={Status}.",
            application.ApplicationId, application.ApplicationStatus);

        return new CreateApplicationResponseDto
        {
            ApplicationId     = application.ApplicationId,
            ApplicationStatus = application.ApplicationStatus,
            CreatedAt         = application.CreatedAt,
            Message           = "Hồ sơ đã được tạo thành công với trạng thái DRAFT. " +
                                "Vui lòng upload giấy tờ và nộp hồ sơ."
        };
    }

    public async Task<bool> HasActiveApplicationAsync(Guid applicantId)
    {
        return await _applicationRepo.HasActiveApplicationAsync(applicantId);
    }

    // ─────────────────────────────────────────────────────────────
    // Cập nhật hồ sơ (DRAFT / NEED_MORE_DOCUMENTS)
    // ─────────────────────────────────────────────────────────────

    public async Task<ApplicationDetailResponseDto> UpdateApplicationAsync(
        Guid applicantId,
        Guid applicationId,
        UpdateApplicationRequestDto request)
    {
        _logger.LogInformation(
            "Applicant {ApplicantId} đang cập nhật hồ sơ {ApplicationId}.",
            applicantId, applicationId);

        var application = await _applicationRepo.GetByIdWithDetailsAsync(applicationId);
        if (application is null)
            throw new ApplicationNotFoundException(applicationId);

        if (application.ApplicantId != applicantId)
            throw new UnauthorizedAccessException("Bạn không có quyền cập nhật hồ sơ này.");

        var editableStatuses = new[]
        {
            ApplicationStatusConstants.Draft,
            ApplicationStatusConstants.NeedMoreDocuments
        };

        if (!editableStatuses.Contains(application.ApplicationStatus))
        {
            throw new ArgumentException(
                $"Chỉ được cập nhật hồ sơ ở trạng thái DRAFT hoặc NEED_MORE_DOCUMENTS. Hiện tại: {application.ApplicationStatus}.");
        }

        if (!HousingStatusConstants.IsValid(request.HousingStatus))
        {
            throw new ArgumentException(
                $"Thực trạng nhà ở '{request.HousingStatus}' không hợp lệ. " +
                $"Giá trị cho phép: {string.Join(", ", HousingStatusConstants.AllValues)}");
        }

        if (!PriorityGroupConstants.IsValid(request.PriorityGroup))
        {
            throw new ArgumentException(
                "Đối tượng phải là hộ nghèo đô thị (URBAN_POOR) hoặc hộ cận nghèo đô thị (URBAN_NEAR_POOR).");
        }

        application.FullName              = request.FullName.Trim();
        application.CitizenId             = request.CitizenId.Trim();
        application.Occupation            = request.Occupation?.Trim();
        application.WorkPlace             = request.WorkPlace?.Trim();
        application.CurrentResidence      = request.CurrentResidence.Trim();
        application.PermanentAddress      = request.PermanentAddress.Trim();
        application.HousingStatus         = request.HousingStatus;
        application.MaritalStatus         = request.MaritalStatus.Trim();
        application.PriorityGroup         = request.PriorityGroup.Trim();
        application.MonthlyIncome         = request.MonthlyIncome;
        application.SpouseMonthlyIncome   = request.SpouseMonthlyIncome;
        application.AverageHousingAreaPerPerson = request.AverageHousingAreaPerPerson;
        application.UpdatedAt             = DateTime.UtcNow;

        // Replace danh sách thành viên hộ gia đình nếu request chứa members
        if (request.HouseholdMembers != null)
        {
            // Xóa members cũ
            var existingMembers = await _context.HouseholdMembers
                .Where(m => m.ApplicationId == applicationId)
                .ToListAsync();
            _context.HouseholdMembers.RemoveRange(existingMembers);

            // Validate và thêm members mới
            foreach (var memberDto in request.HouseholdMembers)
            {
                ValidateMemberRequest(memberDto);
            }

            var newMembers = MapHouseholdMembers(request.HouseholdMembers);
            foreach (var member in newMembers)
            {
                member.ApplicationId = applicationId;
                _context.HouseholdMembers.Add(member);
            }

            application.HouseholdMembersCount = 1 + request.HouseholdMembers.Count;
        }

        await _applicationRepo.UpdateAsync(application);

        _logger.LogInformation(
            "Cập nhật hồ sơ thành công. ApplicationId={ApplicationId}.",
            application.ApplicationId);

        // Reload to ensure navigation props for detail DTO
        var updated = await _applicationRepo.GetByIdWithDetailsAsync(applicationId)
            ?? throw new ApplicationNotFoundException(applicationId);

        var dto = MapToDetailDto(updated);
        dto.Eligibility = await _eligibilityEngine.GetLatestForApplicationAsync(applicationId);
        return dto;
    }

    // ─────────────────────────────────────────────────────────────
    // Xem chi tiết hồ sơ
    // ─────────────────────────────────────────────────────────────

    public async Task<ApplicationDetailResponseDto> GetApplicationByIdAsync(Guid applicationId)
    {
        var application = await _applicationRepo.GetByIdWithDetailsAsync(applicationId);

        if (application is null)
        {
            _logger.LogWarning("Không tìm thấy hồ sơ ApplicationId={ApplicationId}.", applicationId);
            throw new ApplicationNotFoundException(applicationId);
        }

        var dto = MapToDetailDto(application);
        dto.Eligibility = await _eligibilityEngine.GetLatestForApplicationAsync(applicationId);
        return dto;
    }

    // ─────────────────────────────────────────────────────────────
    // Danh sách hồ sơ
    // ─────────────────────────────────────────────────────────────

    public async Task<PagedResultDto<ApplicationSummaryResponseDto>> GetMyApplicationsAsync(
        Guid applicantId,
        ApplicationFilterRequestDto filter)
    {
        NormalizeFilter(filter);
        return await _applicationRepo.GetByApplicantAsync(applicantId, filter);
    }

    public async Task<PagedResultDto<ApplicationSummaryResponseDto>> GetAllApplicationsAsync(
        ApplicationFilterRequestDto filter)
    {
        NormalizeFilter(filter);
        return await _applicationRepo.GetAllAsync(filter);
    }

    // ─────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────

    /// <summary>Chuẩn hóa các tham số phân trang để tránh giá trị không hợp lệ.</summary>
    private static void NormalizeFilter(ApplicationFilterRequestDto filter)
    {
        if (filter.PageIndex < 1) filter.PageIndex = 1;
        filter.PageSize = Math.Clamp(filter.PageSize, 1, 50);
    }

    /// <summary>Map HousingApplication entity → ApplicationDetailResponseDto.</summary>
    private static ApplicationDetailResponseDto MapToDetailDto(HousingApplication app)
    {
        return new ApplicationDetailResponseDto
        {
            // ── Thông tin hồ sơ ───────────────────────────────────
            ApplicationId     = app.ApplicationId,
            ApplicationStatus = app.ApplicationStatus,
            PriorityScore     = app.PriorityScore,
            CreatedAt         = app.CreatedAt,
            SubmittedAt       = app.SubmittedAt,
            UpdatedAt         = app.UpdatedAt,
            FinalDecisionDate = app.FinalDecisionDate,

            // ── Thông tin dự án ───────────────────────────────────
            ProjectId   = app.ProjectId,
            ProjectName = app.HousingProject?.ProjectName ?? string.Empty,

            // ── Thông tin người đăng ký ───────────────────────────
            ApplicantId            = app.ApplicantId,
            FullName               = app.FullName,
            CitizenId              = app.CitizenId,
            Occupation             = app.Occupation,
            WorkPlace              = app.WorkPlace,
            CurrentResidence       = app.CurrentResidence,
            PermanentAddress       = app.PermanentAddress,
            HousingStatus          = app.HousingStatus,
            MaritalStatus          = app.MaritalStatus,
            HouseholdMembersCount  = app.HouseholdMembersCount,
            PriorityGroup          = app.PriorityGroup,
            ReceiptUrl             = app.ReceiptUrl,
            SlotCode               = app.SlotCode,
            LotteryResult          = app.LotteryResult,
            MonthlyIncome          = app.MonthlyIncome,
            SpouseMonthlyIncome    = app.SpouseMonthlyIncome,
            AverageHousingAreaPerPerson = app.AverageHousingAreaPerPerson,
            IsViolation            = app.IsViolation,
            ViolationReason        = app.ViolationReason,

            // ── Cán bộ thẩm định ──────────────────────────────────
            OfficerId      = app.OfficerId,
            OfficerFullName = app.Officer?.FullName,

            // ── Danh sách tài liệu ────────────────────────────────
            Documents = app.Documents.Select(d => new ApplicationDocumentResponseDto
            {
                DocumentId         = d.DocumentId,
                DocumentType       = d.DocumentType,
                FileName           = d.FileName,
                FileUrl            = d.FileUrl,
                FileSizeBytes      = d.FileSizeBytes,
                VerificationStatus = d.VerificationStatus,
                AiRejectedReason   = d.VerificationResult?.ErrorDetails,
                UploadedAt         = d.UploadedAt,
                UploadedBy         = d.UploadedBy
            }).ToList(),

            // ── Lịch sử xét duyệt ────────────────────────────────
            ReviewHistories = app.StatusHistories
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => new ReviewHistoryResponseDto
                {
                    HistoryId        = h.HistoryId,
                    Action           = h.Action,
                    OldStatus        = h.OldStatus,
                    NewStatus        = h.NewStatus,
                    Note             = h.Note,
                    ChangedAt        = h.ChangedAt,
                    ChangedBy        = h.ChangedBy,
                    ChangedByFullName = h.ChangedByUser?.FullName ?? string.Empty
                }).ToList(),

            // ── Thành viên hộ gia đình ──────────────────────────────────
            HouseholdMembers = app.HouseholdMembers
                .Select(m => new HouseholdMemberResponseDto
                {
                    MemberId     = m.MemberId,
                    FullName     = m.FullName,
                    CitizenId    = m.CitizenId,
                    DateOfBirth  = m.DateOfBirth,
                    Relationship = m.Relationship,
                    Note         = m.Note,
                    CreatedAt    = m.CreatedAt
                }).ToList()
        };
    }

    public async Task<PagedResult<HousingApplicationDashboardItemDto>> GetHousingDeveloperDashboardAsync(
        HousingApplicationDashboardQueryDto query)
    {
        NormalizeDashboardQuery(query);
        return await _applicationRepo.GetHousingDeveloperDashboardAsync(query);
    }

    public async Task<PagedResult<HousingApplicationDashboardItemDto>> GetDepartmentOfConstructionDashboardAsync(
        HousingApplicationDashboardQueryDto query)
    {
        NormalizeDashboardQuery(query);
        return await _applicationRepo.GetDepartmentOfConstructionDashboardAsync(query);
    }

    private static void NormalizeDashboardQuery(HousingApplicationDashboardQueryDto query)
    {
        if (query.PageIndex < 1) query.PageIndex = 1;
        query.PageSize = Math.Clamp(query.PageSize, 1, 50);
    }

    // ─────────────────────────────────────────────────────────────
    // Final List (Task #10)
    // ─────────────────────────────────────────────────────────────

    public async Task<List<FinalListItemDto>> GetFinalListByProjectAsync(Guid projectId)
    {
        return await _applicationRepo.GetFinalListByProjectAsync(projectId);
    }

    // ─────────────────────────────────────────────────────────────
    // Household Members CRUD
    // ─────────────────────────────────────────────────────────────

    public async Task<List<HouseholdMemberResponseDto>> GetMembersByApplicationIdAsync(
        Guid applicantId, Guid applicationId)
    {
        var application = await _applicationRepo.GetByIdWithDetailsAsync(applicationId)
            ?? throw new ApplicationNotFoundException(applicationId);

        // Applicant chỉ xem của mình, Officer xem được tất cả
        // (Controller sẽ xử lý logic phân quyền chi tiết)

        return application.HouseholdMembers
            .Select(MapToMemberResponseDto)
            .ToList();
    }

    public async Task<HouseholdMemberResponseDto> AddMemberAsync(
        Guid applicantId, Guid applicationId, HouseholdMemberRequestDto request)
    {
        var application = await GetEditableApplication(applicantId, applicationId);

        ValidateMemberRequest(request);

        var now = DateTime.UtcNow;
        var member = new HouseholdMember
        {
            MemberId      = Guid.NewGuid(),
            ApplicationId = applicationId,
            FullName      = request.FullName.Trim(),
            CitizenId     = request.CitizenId?.Trim(),
            DateOfBirth   = request.DateOfBirth,
            Relationship  = request.Relationship.Trim().ToUpperInvariant(),
            Note          = request.Note?.Trim(),
            CreatedAt     = now
        };

        _context.HouseholdMembers.Add(member);

        // Auto-update count
        application.HouseholdMembersCount = 1 + await _context.HouseholdMembers
            .CountAsync(m => m.ApplicationId == applicationId) + 1; // +1 for the new member not yet saved
        application.UpdatedAt = now;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Thêm thành viên {MemberId} vào hồ sơ {ApplicationId}. Tổng: {Count} người.",
            member.MemberId, applicationId, application.HouseholdMembersCount);

        return MapToMemberResponseDto(member);
    }

    public async Task<HouseholdMemberResponseDto> UpdateMemberAsync(
        Guid applicantId, Guid applicationId, Guid memberId, HouseholdMemberRequestDto request)
    {
        await GetEditableApplication(applicantId, applicationId);

        ValidateMemberRequest(request);

        var member = await _context.HouseholdMembers
            .FirstOrDefaultAsync(m => m.MemberId == memberId && m.ApplicationId == applicationId)
            ?? throw new KeyNotFoundException(
                $"Không tìm thấy thành viên {memberId} trong hồ sơ {applicationId}.");

        member.FullName     = request.FullName.Trim();
        member.CitizenId    = request.CitizenId?.Trim();
        member.DateOfBirth  = request.DateOfBirth;
        member.Relationship = request.Relationship.Trim().ToUpperInvariant();
        member.Note         = request.Note?.Trim();
        member.UpdatedAt    = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Cập nhật thành viên {MemberId} trong hồ sơ {ApplicationId}.",
            memberId, applicationId);

        return MapToMemberResponseDto(member);
    }

    public async Task RemoveMemberAsync(
        Guid applicantId, Guid applicationId, Guid memberId)
    {
        var application = await GetEditableApplication(applicantId, applicationId);

        var member = await _context.HouseholdMembers
            .FirstOrDefaultAsync(m => m.MemberId == memberId && m.ApplicationId == applicationId)
            ?? throw new KeyNotFoundException(
                $"Không tìm thấy thành viên {memberId} trong hồ sơ {applicationId}.");

        _context.HouseholdMembers.Remove(member);

        // Auto-update count
        var remainingCount = await _context.HouseholdMembers
            .CountAsync(m => m.ApplicationId == applicationId && m.MemberId != memberId);
        application.HouseholdMembersCount = 1 + remainingCount;
        application.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Xóa thành viên {MemberId} khỏi hồ sơ {ApplicationId}. Tổng còn: {Count} người.",
            memberId, applicationId, application.HouseholdMembersCount);
    }

    // ─────────────────────────────────────────────────────────────
    // Household Members Helpers
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy hồ sơ và kiểm tra quyền chỉnh sửa (DRAFT / NEED_MORE_DOCUMENTS, chủ hồ sơ).
    /// </summary>
    private async Task<HousingApplication> GetEditableApplication(Guid applicantId, Guid applicationId)
    {
        var application = await _context.HousingApplications
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId)
            ?? throw new ApplicationNotFoundException(applicationId);

        if (application.ApplicantId != applicantId)
            throw new UnauthorizedAccessException("Bạn không có quyền thực hiện thao tác này trên hồ sơ.");

        var editableStatuses = new[]
        {
            ApplicationStatusConstants.Draft,
            ApplicationStatusConstants.NeedMoreDocuments
        };

        if (!editableStatuses.Contains(application.ApplicationStatus))
        {
            throw new ArgumentException(
                $"Chỉ được chỉnh sửa thành viên khi hồ sơ ở trạng thái DRAFT hoặc NEED_MORE_DOCUMENTS. Hiện tại: {application.ApplicationStatus}.");
        }

        return application;
    }

    /// <summary>Validate thông tin thành viên: relationship hợp lệ, CCCD bắt buộc nếu ≥ 14 tuổi.</summary>
    private static void ValidateMemberRequest(HouseholdMemberRequestDto request)
    {
        if (!HouseholdRelationshipConstants.IsValid(request.Relationship))
        {
            throw new ArgumentException(
                $"Quan hệ '{request.Relationship}' không hợp lệ. " +
                $"Giá trị cho phép: {string.Join(", ", HouseholdRelationshipConstants.AllValues)}");
        }

        // Luật VN: từ 14 tuổi trở lên bắt buộc có CCCD
        if (request.DateOfBirth.HasValue)
        {
            var age = DateTime.UtcNow.Year - request.DateOfBirth.Value.Year;
            if (request.DateOfBirth.Value > DateTime.UtcNow.AddYears(-age))
                age--;

            if (age >= 14 && string.IsNullOrWhiteSpace(request.CitizenId))
            {
                throw new ArgumentException(
                    $"Thành viên '{request.FullName}' từ 14 tuổi trở lên bắt buộc phải có số CCCD (theo luật Việt Nam).");
            }
        }
    }

    /// <summary>Map danh sách HouseholdMemberRequestDto → HouseholdMember entities.</summary>
    private static List<HouseholdMember> MapHouseholdMembers(
        List<HouseholdMemberRequestDto>? memberDtos)
    {
        if (memberDtos == null || memberDtos.Count == 0)
            return new List<HouseholdMember>();

        var now = DateTime.UtcNow;
        return memberDtos.Select(dto =>
        {
            ValidateMemberRequest(dto);
            return new HouseholdMember
            {
                MemberId     = Guid.NewGuid(),
                FullName     = dto.FullName.Trim(),
                CitizenId    = dto.CitizenId?.Trim(),
                DateOfBirth  = dto.DateOfBirth,
                Relationship = dto.Relationship.Trim().ToUpperInvariant(),
                Note         = dto.Note?.Trim(),
                CreatedAt    = now
            };
        }).ToList();
    }

    private static HouseholdMemberResponseDto MapToMemberResponseDto(HouseholdMember m)
    {
        return new HouseholdMemberResponseDto
        {
            MemberId     = m.MemberId,
            FullName     = m.FullName,
            CitizenId    = m.CitizenId,
            DateOfBirth  = m.DateOfBirth,
            Relationship = m.Relationship,
            Note         = m.Note,
            CreatedAt    = m.CreatedAt
        };
    }

    /// <inheritdoc/>
    public async Task<ProjectApplicationEvaluationDto> GetProjectApplicationEvaluationAsync(Guid projectId)
    {
        var project = await _context.HousingProjects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId)
            ?? throw new InvalidOperationException("Không tìm thấy dự án.");

        var qualifiedStatuses = new[]
        {
            ApplicationStatusConstants.Approved,
            ApplicationStatusConstants.ApprovedByTimeout
        };

        var apps = await _context.HousingApplications
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId && qualifiedStatuses.Contains(a.ApplicationStatus) && !a.IsViolation)
            .OrderByDescending(a => a.PriorityScore)
            .ThenBy(a => a.SubmittedAt)
            .ToListAsync();

        var priorityApps = apps.Where(a => !string.IsNullOrWhiteSpace(a.PriorityGroup)).Select(MapToSummaryItem).ToList();
        var nonPriorityApps = apps.Where(a => string.IsNullOrWhiteSpace(a.PriorityGroup)).Select(MapToSummaryItem).ToList();

        var scenario = apps.Count <= project.AvailableUnits ? "LESS_OR_EQUAL_AVAILABLE" : "GREATER_THAN_AVAILABLE";

        return new ProjectApplicationEvaluationDto
        {
            ProjectId = projectId,
            ProjectName = project.ProjectName,
            AvailableUnits = project.AvailableUnits,
            TotalQualifiedApplications = apps.Count,
            PriorityCount = priorityApps.Count,
            NonPriorityCount = nonPriorityApps.Count,
            RecommendedScenario = scenario,
            PriorityApplications = priorityApps,
            NonPriorityApplications = nonPriorityApps
        };
    }

    /// <inheritdoc/>
    public async Task<bool> ExecuteDeveloperDecisionAsync(
        Guid projectId, DeveloperWorkflowDecisionRequestDto request, Guid developerUserId)
    {
        var project = await _context.HousingProjects
            .Include(p => p.HousingProjectStatus)
            .FirstOrDefaultAsync(p => p.Id == projectId)
            ?? throw new InvalidOperationException("Không tìm thấy dự án.");

        var qualifiedStatuses = new[]
        {
            ApplicationStatusConstants.Approved,
            ApplicationStatusConstants.ApprovedByTimeout
        };

        var apps = await _context.HousingApplications
            .Include(a => a.PrincipleAgreement)
            .Where(a => a.ProjectId == projectId && qualifiedStatuses.Contains(a.ApplicationStatus) && !a.IsViolation)
            .OrderByDescending(a => a.PriorityScore)
            .ThenBy(a => a.SubmittedAt)
            .ToListAsync();

        var now = DateTime.UtcNow;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (request.DecisionType == "CLOSE_AND_SIGN")
            {
                foreach (var app in apps)
                {
                    var oldStatus = app.ApplicationStatus;
                    app.ApplicationStatus = ApplicationStatusConstants.ContractPending;
                    app.UpdatedAt = now;

                    if (app.PrincipleAgreement == null)
                    {
                        var agreement = new PrincipleAgreement
                        {
                            Id = Guid.NewGuid(),
                            ApplicationId = app.ApplicationId,
                            PdfUrl = $"/api/payment/download-contract/{app.ApplicationId}",
                            CreatedAt = now
                        };
                        await _context.PrincipleAgreements.AddAsync(agreement);
                    }

                    _context.ApplicationStatusHistories.Add(new ApplicationStatusHistory
                    {
                        HistoryId = Guid.NewGuid(),
                        ApplicationId = app.ApplicationId,
                        ChangedBy = developerUserId,
                        Action = ReviewActionConstants.DeveloperDecisionCloseAndSign,
                        OldStatus = oldStatus,
                        NewStatus = ApplicationStatusConstants.ContractPending,
                        Note = "CĐT chốt danh sách đủ điều kiện, chuyển sang bước ký hợp đồng nguyên tắc.",
                        ChangedAt = now
                    });
                }

                if (request.CloseProject)
                {
                    var closedStatus = await _context.HousingProjectStatuses
                        .FirstOrDefaultAsync(s => s.StatusCode == "CLOSED");
                    if (closedStatus != null)
                    {
                        project.HousingProjectStatusId = closedStatus.Id;
                        project.HousingProjectStatus = closedStatus;
                        project.UpdatedAt = now;
                    }
                }
            }
            else if (request.DecisionType == "KEEP_OPEN")
            {
                foreach (var app in apps)
                {
                    _context.ApplicationStatusHistories.Add(new ApplicationStatusHistory
                    {
                        HistoryId = Guid.NewGuid(),
                        ApplicationId = app.ApplicationId,
                        ChangedBy = developerUserId,
                        Action = ReviewActionConstants.DeveloperDecisionKeepOpen,
                        OldStatus = app.ApplicationStatus,
                        NewStatus = app.ApplicationStatus,
                        Note = "CĐT lưu hồ sơ đạt yêu cầu và tiếp tục mở tiếp nhận thêm hồ sơ đợt tới.",
                        ChangedAt = now
                    });
                }
            }
            else if (request.DecisionType == "PROCESS_PRIORITY_AND_LOTTERY")
            {
                var priorityApps = apps.Where(a => !string.IsNullOrWhiteSpace(a.PriorityGroup)).ToList();
                List<HousingApplication> selectedPriority = new();

                if (priorityApps.Count <= project.AvailableUnits)
                {
                    selectedPriority = priorityApps;
                }
                else
                {
                    if (request.SelectedPriorityApplicationIds != null && request.SelectedPriorityApplicationIds.Count > 0)
                    {
                        selectedPriority = priorityApps
                            .Where(a => request.SelectedPriorityApplicationIds.Contains(a.ApplicationId))
                            .Take(project.AvailableUnits)
                            .ToList();
                    }
                    else
                    {
                        selectedPriority = priorityApps.Take(project.AvailableUnits).ToList();
                    }
                }

                foreach (var app in selectedPriority)
                {
                    var oldStatus = app.ApplicationStatus;
                    app.ApplicationStatus = ApplicationStatusConstants.ContractPending;
                    app.UpdatedAt = now;

                    if (app.PrincipleAgreement == null)
                    {
                        var agreement = new PrincipleAgreement
                        {
                            Id = Guid.NewGuid(),
                            ApplicationId = app.ApplicationId,
                            PdfUrl = $"/api/payment/download-contract/{app.ApplicationId}",
                            CreatedAt = now
                        };
                        await _context.PrincipleAgreements.AddAsync(agreement);
                    }

                    _context.ApplicationStatusHistories.Add(new ApplicationStatusHistory
                    {
                        HistoryId = Guid.NewGuid(),
                        ApplicationId = app.ApplicationId,
                        ChangedBy = developerUserId,
                        Action = ReviewActionConstants.PriorityDirectApproval,
                        OldStatus = oldStatus,
                        NewStatus = ApplicationStatusConstants.ContractPending,
                        Note = "Duyệt trực tiếp đối tượng thuộc diện ưu tiên (không qua bốc thăm), chuyển sang bước ký hợp đồng nguyên tắc.",
                        ChangedAt = now
                    });
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Lỗi xảy ra khi thực thi quyết định của CĐT cho dự án {ProjectId}", projectId);
            throw;
        }
    }

    private static ApplicationSummaryItemDto MapToSummaryItem(HousingApplication a)
    {
        return new ApplicationSummaryItemDto
        {
            ApplicationId = a.ApplicationId,
            FullName = a.FullName,
            CitizenId = a.CitizenId,
            PriorityGroup = a.PriorityGroup,
            PriorityScore = a.PriorityScore,
            SubmittedAt = a.SubmittedAt,
            ApplicationStatus = a.ApplicationStatus
        };
    }
}
