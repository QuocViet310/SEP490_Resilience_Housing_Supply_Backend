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
    private readonly ILogger<HousingApplicationService> _logger;

    public HousingApplicationService(
        IHousingApplicationRepository applicationRepo,
        ILogger<HousingApplicationService> logger)
    {
        _applicationRepo = applicationRepo;
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
            PriorityGroup          = request.PriorityGroup?.Trim()
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

        return MapToDetailDto(application);
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
}
