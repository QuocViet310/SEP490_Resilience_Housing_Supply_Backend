using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.HousingApplications;
using RHS.Application.DTOs.HousingApplications.Dashboard;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Exceptions;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Service xử lý nghiệp vụ tạo hồ sơ và xem hồ sơ nhà ở xã hội.
/// </summary>
public class HousingApplicationService : IHousingApplicationService
{
    private readonly IHousingApplicationRepository _applicationRepo;
    private readonly IEligibilityRuleEngine _eligibilityEngine;
    private readonly ILogger<HousingApplicationService> _logger;

    public HousingApplicationService(
        IHousingApplicationRepository applicationRepo,
        IEligibilityRuleEngine eligibilityEngine,
        ILogger<HousingApplicationService> logger)
    {
        _applicationRepo = applicationRepo;
        _eligibilityEngine = eligibilityEngine;
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
            HouseholdMembersCount  = request.HouseholdMembersCount,
            PriorityGroup          = request.PriorityGroup.Trim(),
            MonthlyIncome          = request.MonthlyIncome,
            SpouseMonthlyIncome    = request.SpouseMonthlyIncome,
            AverageHousingAreaPerPerson = request.AverageHousingAreaPerPerson,
            LotteryResult          = LotteryResultConstants.Pending
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
        application.HouseholdMembersCount = request.HouseholdMembersCount;
        application.PriorityGroup         = request.PriorityGroup.Trim();
        application.MonthlyIncome         = request.MonthlyIncome;
        application.SpouseMonthlyIncome   = request.SpouseMonthlyIncome;
        application.AverageHousingAreaPerPerson = request.AverageHousingAreaPerPerson;
        application.UpdatedAt             = DateTime.UtcNow;

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
}
