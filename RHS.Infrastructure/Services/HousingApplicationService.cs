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
using RHS.Infrastructure.Helpers;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Service xử lý nghiệp vụ tạo hồ sơ và xem hồ sơ nhà ở xã hội.
/// </summary>
public class HousingApplicationService : IHousingApplicationService
{
    private readonly IHousingApplicationRepository _applicationRepo;
    private readonly IEligibilityRuleEngine _eligibilityEngine;
    private readonly INotificationService _notificationService;
    private readonly IInstallmentService _installmentService;
    private readonly AppDbContext _context;
    private readonly ILogger<HousingApplicationService> _logger;

    public HousingApplicationService(
        IHousingApplicationRepository applicationRepo,
        IEligibilityRuleEngine eligibilityEngine,
        INotificationService notificationService,
        IInstallmentService installmentService,
        AppDbContext context,
        ILogger<HousingApplicationService> logger)
    {
        _applicationRepo = applicationRepo;
        _eligibilityEngine = eligibilityEngine;
        _notificationService = notificationService;
        _installmentService = installmentService;
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

        var now = DateTime.UtcNow;

        // 1. Kiểm tra dự án tồn tại và thời gian mở nhận hồ sơ
        var project = await _context.HousingProjects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

        if (project == null)
            throw new KeyNotFoundException($"Không tìm thấy dự án với ID: {request.ProjectId}");

        if (project.ApplicationOpenDate.HasValue && now < project.ApplicationOpenDate.Value)
        {
            throw new ArgumentException(
                $"Dự án chưa đến thời gian mở nhận hồ sơ (Thời gian mở: {project.ApplicationOpenDate.Value:dd/MM/yyyy HH:mm}).");
        }

        if (project.ApplicationCloseDate.HasValue && now > project.ApplicationCloseDate.Value)
        {
            throw new ArgumentException(
                $"Dự án đã kết thúc thời hạn nhận hồ sơ (Hạn chót: {project.ApplicationCloseDate.Value:dd/MM/yyyy HH:mm}).");
        }

        // 2. Kiểm tra trùng lặp & Chống nộp nhiều nơi (Active App Check)
        var alreadyExists = await _applicationRepo.ExistsByApplicantAndProjectAsync(
            applicantId, request.ProjectId);

        if (alreadyExists)
        {
            _logger.LogWarning(
                "Applicant {ApplicantId} đã có hồ sơ cho dự án {ProjectId}.",
                applicantId, request.ProjectId);
            throw new DuplicateApplicationException(applicantId, request.ProjectId);
        }

        var hasActiveApplication = await _applicationRepo.HasActiveApplicationAsync(applicantId);
        if (hasActiveApplication)
        {
            _logger.LogWarning(
                "Applicant {ApplicantId} đã có hồ sơ đang hoạt động ở một dự án khác. Không thể tạo đơn mới.",
                applicantId);
            throw new ActiveApplicationExistsException(applicantId);
        }

        // 3. Tự động trích xuất thông tin từ Hồ sơ cá nhân (Profile) nếu AutoFillFromProfile = true
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == applicantId);

        var fullName         = string.IsNullOrWhiteSpace(request.FullName) ? user?.FullName ?? string.Empty : request.FullName.Trim();
        var citizenId        = string.IsNullOrWhiteSpace(request.CitizenId) ? user?.CitizenId ?? string.Empty : request.CitizenId.Trim();
        var currentResidence = string.IsNullOrWhiteSpace(request.CurrentResidence) ? (user?.CurrentResidence ?? user?.Address ?? string.Empty) : request.CurrentResidence.Trim();
        var permanentAddress = string.IsNullOrWhiteSpace(request.PermanentAddress) ? (user?.PermanentAddress ?? user?.Address ?? string.Empty) : request.PermanentAddress.Trim();
        var occupation       = string.IsNullOrWhiteSpace(request.Occupation) ? user?.Occupation?.Trim() : request.Occupation?.Trim();
        var workPlace        = string.IsNullOrWhiteSpace(request.WorkPlace) ? user?.WorkPlace?.Trim() : request.WorkPlace?.Trim();
        var maritalStatus    = string.IsNullOrWhiteSpace(request.MaritalStatus) ? (user?.MaritalStatus ?? MaritalStatusConstants.Single) : request.MaritalStatus.Trim().ToUpperInvariant();
        var housingStatus    = string.IsNullOrWhiteSpace(request.HousingStatus) ? (user?.HousingStatus ?? HousingStatusConstants.NoHouse) : request.HousingStatus.Trim().ToUpperInvariant();
        var priorityGroup    = string.IsNullOrWhiteSpace(request.PriorityGroup) ? (user?.PriorityGroup ?? PriorityGroupConstants.LowIncomeUrban) : request.PriorityGroup.Trim().ToUpperInvariant();
        var monthlyIncome    = request.MonthlyIncome ?? user?.MonthlyIncome;
        var spouseIncome     = request.SpouseMonthlyIncome ?? user?.SpouseMonthlyIncome;

        // Validate cơ bản
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(citizenId))
        {
            throw new ArgumentException("Họ tên và số CCCD là bắt buộc. Vui lòng hoàn tất eKYC/Profile hoặc điền vào đơn.");
        }

        if (!HousingStatusConstants.IsValid(housingStatus))
        {
            throw new ArgumentException(
                $"Thực trạng nhà ở '{housingStatus}' không hợp lệ. Cho phép: {string.Join(", ", HousingStatusConstants.AllValues)}");
        }

        if (!PriorityGroupConstants.IsValid(priorityGroup))
        {
            throw new ArgumentException(
                $"Đối tượng thụ hưởng '{priorityGroup}' không hợp lệ. Vui lòng chọn nhóm theo Điều 76 Luật Nhà ở 2023.");
        }

        // 4. Kế thừa nhân khẩu hộ gia đình
        List<HouseholdMember> initialMembers;
        if (request.HouseholdMembers != null && request.HouseholdMembers.Count > 0)
        {
            initialMembers = MapHouseholdMembers(request.HouseholdMembers);
        }
        else
        {
            var savedMembers = await _context.UserHouseholdMembers
                .AsNoTracking()
                .Where(m => m.UserId == applicantId)
                .ToListAsync();

            initialMembers = savedMembers.Select(m => new HouseholdMember
            {
                MemberId        = Guid.NewGuid(),
                FullName        = m.FullName,
                CitizenId       = m.CitizenId,
                DateOfBirth     = m.DateOfBirth,
                Relationship    = m.Relationship,
                Occupation      = m.Occupation,
                MonthlyIncome   = m.MonthlyIncome,
                IsDependent     = m.IsDependent,
                DependentReason = m.DependentReason,
                HasMeritService = m.HasMeritService,
                MeritDetails    = m.MeritDetails,
                Note            = m.Note,
                CreatedAt       = now
            }).ToList();
        }

        // 5. Tính toán diện tích bình quân đầu người
        var totalPeople = 1 + (maritalStatus == MaritalStatusConstants.Married ? 1 : 0) + initialMembers.Count;
        decimal? avgArea = request.AverageHousingAreaPerPerson ?? user?.AverageHousingAreaPerPerson;
        if (request.TotalHousingArea.HasValue && request.TotalHousingArea.Value > 0)
        {
            avgArea = (decimal)Math.Round(request.TotalHousingArea.Value / totalPeople, 2);
        }

        // 6. Khởi tạo thực thể HousingApplication
        var application = new HousingApplication
        {
            ApplicationId               = Guid.NewGuid(),
            ApplicantId                 = applicantId,
            ProjectId                   = request.ProjectId,
            ApplicationStatus           = ApplicationStatusConstants.Draft,
            CreatedAt                   = now,
            SubmittedAt                 = now,
            PriorityScore               = 0,
            FinalDecisionDate           = null,
            FullName                    = fullName,
            CitizenId                   = citizenId,
            Occupation                  = occupation,
            WorkPlace                   = workPlace,
            CurrentResidence            = currentResidence,
            PermanentAddress            = permanentAddress,
            HousingStatus               = housingStatus,
            MaritalStatus               = maritalStatus,
            HouseholdMembersCount       = totalPeople,
            PriorityGroup               = priorityGroup,
            MonthlyIncome               = monthlyIncome,
            SpouseMonthlyIncome         = spouseIncome,
            AverageHousingAreaPerPerson = avgArea,
            LotteryResult               = LotteryResultConstants.Pending,
            DesiredApartmentTypeId      = await ResolveDesiredApartmentTypeIdAsync(request.DesiredApartmentTypeId, request.DesiredApartmentType),
            HouseholdMembers            = initialMembers
        };

        // 7. Kế thừa tài liệu từ Kho hồ sơ cá nhân (Document Vault)
        var inheritedDocCount = 0;
        if (request.InheritDocumentsFromVault)
        {
            var vaultDocs = await _context.UserDocuments
                .AsNoTracking()
                .Where(d => d.UserId == applicantId)
                .ToListAsync();

            var docsByType = vaultDocs
                .Where(d => DocumentTypeConstants.AllowedApplicantDocumentTypes.Contains(d.DocumentType))
                .GroupBy(d => d.DocumentType)
                .Select(g => g.OrderByDescending(x => x.UploadedAt).First())
                .ToList();

            foreach (var vd in docsByType)
            {
                application.Documents.Add(new ApplicationDocument
                {
                    DocumentId         = Guid.NewGuid(),
                    ApplicationId      = application.ApplicationId,
                    UploadedBy         = applicantId,
                    DocumentType       = vd.DocumentType,
                    FileName           = vd.FileName,
                    FileUrl            = vd.FileUrl,
                    FileSizeBytes      = vd.FileSizeBytes,
                    UploadedAt         = now,
                    VerificationStatus = "PENDING"
                });
            }
            inheritedDocCount = application.Documents.Count;
        }

        try
        {
            await _applicationRepo.CreateAsync(application);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(
                ex,
                "Unique constraint khi tạo hồ sơ Applicant {ApplicantId} Project {ProjectId}.",
                applicantId, request.ProjectId);
            throw new DuplicateApplicationException(applicantId, request.ProjectId);
        }

        // 8. Đánh giá tự động sơ bộ điều kiện NOXH (Rule Engine: <15tr/người, <10m2/người)
        var eligibility = await _eligibilityEngine.AssessAsync(application);

        _logger.LogInformation(
            "Tạo hồ sơ thành công. ApplicationId={ApplicationId}, Eligible={Eligible}, Score={Score}, InhertedDocs={DocCount}.",
            application.ApplicationId, eligibility.Eligible, eligibility.EstimatedScore, inheritedDocCount);

        var statusMessage = eligibility.Eligible
            ? $"Hồ sơ tạo thành công (Trạng thái: DRAFT). {eligibility.SummaryMessage}"
            : $"Hồ sơ tạo thành công (Trạng thái: DRAFT). CẢNH BÁO: {eligibility.SummaryMessage}";

        return new CreateApplicationResponseDto
        {
            ApplicationId         = application.ApplicationId,
            ApplicationStatus     = application.ApplicationStatus,
            CreatedAt             = application.CreatedAt,
            InheritedMembersCount = initialMembers.Count,
            InheritedDocsCount    = inheritedDocCount,
            Eligibility           = eligibility,
            Message               = statusMessage
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
        application.DesiredApartmentTypeId = await ResolveDesiredApartmentTypeIdAsync(request.DesiredApartmentTypeId, request.DesiredApartmentType);
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
            ApartmentId            = app.ApartmentId,
            ApartmentUnitName      = app.Apartment?.UnitName,
            ApartmentType          = app.Apartment?.ApartmentType?.TypeCode,
            ApartmentTypeLabel     = app.Apartment?.ApartmentType?.TypeName,
            ApartmentArea          = app.Apartment?.Area,
            ApartmentPrice         = app.Apartment?.Price,
            ApartmentStatus        = app.Apartment?.Status,
            MonthlyIncome          = app.MonthlyIncome,
            SpouseMonthlyIncome    = app.SpouseMonthlyIncome,
            AverageHousingAreaPerPerson = app.AverageHousingAreaPerPerson,
            IsViolation            = app.IsViolation,
            ViolationReason        = app.ViolationReason,
            DesiredApartmentTypeId = app.DesiredApartmentTypeId,
            DesiredApartmentType   = app.DesiredApartmentType?.TypeCode,
            DesiredApartmentTypeLabel = app.DesiredApartmentType?.TypeName,
            WaitlistNumber         = app.WaitlistNumber,
            WaitlistPromotedAt     = app.WaitlistPromotedAt,
            DepositDeadline        = app.DepositDeadline,

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
            MemberId        = Guid.NewGuid(),
            ApplicationId   = applicationId,
            FullName        = request.FullName.Trim(),
            CitizenId       = request.CitizenId?.Trim(),
            DateOfBirth     = request.DateOfBirth,
            Relationship    = request.Relationship.Trim().ToUpperInvariant(),
            Occupation      = request.Occupation?.Trim(),
            MonthlyIncome   = request.MonthlyIncome,
            IsDependent     = request.IsDependent,
            DependentReason = request.DependentReason?.Trim().ToUpperInvariant(),
            HasMeritService = request.HasMeritService,
            MeritDetails    = request.MeritDetails?.Trim(),
            Note            = request.Note?.Trim(),
            CreatedAt       = now
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

        member.FullName        = request.FullName.Trim();
        member.CitizenId       = request.CitizenId?.Trim();
        member.DateOfBirth     = request.DateOfBirth;
        member.Relationship    = request.Relationship.Trim().ToUpperInvariant();
        member.Occupation      = request.Occupation?.Trim();
        member.MonthlyIncome   = request.MonthlyIncome;
        member.IsDependent     = request.IsDependent;
        member.DependentReason = request.DependentReason?.Trim().ToUpperInvariant();
        member.HasMeritService = request.HasMeritService;
        member.MeritDetails    = request.MeritDetails?.Trim();
        member.Note            = request.Note?.Trim();
        member.UpdatedAt       = DateTime.UtcNow;

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
                MemberId        = Guid.NewGuid(),
                FullName        = dto.FullName.Trim(),
                CitizenId       = dto.CitizenId?.Trim(),
                DateOfBirth     = dto.DateOfBirth,
                Relationship    = dto.Relationship.Trim().ToUpperInvariant(),
                Occupation      = dto.Occupation?.Trim(),
                MonthlyIncome   = dto.MonthlyIncome,
                IsDependent     = dto.IsDependent,
                DependentReason = dto.DependentReason?.Trim().ToUpperInvariant(),
                HasMeritService = dto.HasMeritService,
                MeritDetails    = dto.MeritDetails?.Trim(),
                Note            = dto.Note?.Trim(),
                CreatedAt       = now
            };
        }).ToList();
    }

    private static HouseholdMemberResponseDto MapToMemberResponseDto(HouseholdMember m)
    {
        return new HouseholdMemberResponseDto
        {
            MemberId        = m.MemberId,
            FullName        = m.FullName,
            CitizenId       = m.CitizenId,
            DateOfBirth     = m.DateOfBirth,
            Relationship    = m.Relationship,
            Occupation      = m.Occupation,
            MonthlyIncome   = m.MonthlyIncome,
            IsDependent     = m.IsDependent,
            DependentReason = m.DependentReason,
            HasMeritService = m.HasMeritService,
            MeritDetails    = m.MeritDetails,
            Note            = m.Note,
            CreatedAt       = m.CreatedAt
        };
    }

    /// <inheritdoc/>
    public async Task<ProjectApplicationEvaluationDto> GetProjectApplicationEvaluationAsync(Guid projectId)
    {
        // Căn cứ = đếm căn AVAILABLE − soft-hold (không tin counter cũ)
        var availableUnits = await ProjectUnitSeatHelper.SyncAvailableUnitsAsync(
            _context, projectId, _logger);
        await _context.SaveChangesAsync();

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

        var scenario = apps.Count <= availableUnits ? "LESS_OR_EQUAL_AVAILABLE" : "GREATER_THAN_AVAILABLE";

        return new ProjectApplicationEvaluationDto
        {
            ProjectId = projectId,
            ProjectName = project.ProjectName,
            AvailableUnits = availableUnits,
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
        var pendingNotify = new List<(Guid ApplicantId, string Note)>();
        var appsNeedingInstallments = new List<Guid>();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Đồng bộ suất từ số căn AVAILABLE trước khi quyết định
            await ProjectUnitSeatHelper.SyncAvailableUnitsAsync(_context, projectId, _logger);

            if (request.DecisionType == "CLOSE_AND_SIGN")
            {
                if (project.AvailableUnits <= 0)
                    throw new InvalidOperationException("Dự án đã hết suất. Không thể chốt ký hợp đồng.");

                if (apps.Count > project.AvailableUnits)
                    throw new InvalidOperationException(
                        $"Số hồ sơ đã duyệt ({apps.Count}) vượt số căn còn lại ({project.AvailableUnits}). " +
                        "Hãy dùng quyết định «Ưu tiên + bốc thăm» thay vì «Chốt & ký».");

                await AssignApartmentsForAppsAsync(
                    projectId, apps, request.ApartmentAssignments, developerUserId, now);

                foreach (var app in apps)
                {
                    if (string.IsNullOrWhiteSpace(app.LotteryResult)
                        || app.LotteryResult == LotteryResultConstants.Pending)
                    {
                        app.LotteryResult = !string.IsNullOrWhiteSpace(app.PriorityGroup)
                            ? LotteryResultConstants.PriorityWon
                            : LotteryResultConstants.Won;
                    }

                    MoveToDepositPending(
                        app,
                        developerUserId,
                        now,
                        ReviewActionConstants.DeveloperDecisionCloseAndSign,
                        $"CĐT chốt danh sách + bàn giao căn {app.Apartment?.UnitName ?? ""}, chuyển sang bước thanh toán cọc Đợt 1 (10%).",
                        pendingNotify);
                    appsNeedingInstallments.Add(app.ApplicationId);
                }

                await ProjectUnitSeatHelper.SyncAvailableUnitsAsync(_context, projectId, _logger);
                project.UpdatedAt = now;

                if (request.CloseProject)
                {
                    var closedStatus = await _context.HousingProjectStatuses
                        .FirstOrDefaultAsync(s => s.StatusCode == "CLOSED");
                    if (closedStatus != null)
                    {
                        project.HousingProjectStatusId = closedStatus.Id;
                        project.HousingProjectStatus = closedStatus;
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
                if (apps.Count <= project.AvailableUnits)
                    throw new InvalidOperationException(
                        "Số hồ sơ đã duyệt không vượt số căn. Hãy dùng «Chốt & ký» hoặc «Giữ mở» thay vì bốc thăm.");

                if (project.AvailableUnits <= 0)
                    throw new InvalidOperationException("Dự án đã hết suất. Không thể duyệt ưu tiên / lập lịch bốc thăm.");

                var priorityApps = apps.Where(a => !string.IsNullOrWhiteSpace(a.PriorityGroup)).ToList();
                List<HousingApplication> selectedPriority;

                if (priorityApps.Count <= project.AvailableUnits)
                {
                    selectedPriority = priorityApps;
                }
                else if (request.SelectedPriorityApplicationIds != null && request.SelectedPriorityApplicationIds.Count > 0)
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

                if (selectedPriority.Count == 0)
                    throw new InvalidOperationException("Không có hồ sơ ưu tiên nào được chọn để cấp căn.");

                await AssignApartmentsForAppsAsync(
                    projectId, selectedPriority, request.ApartmentAssignments, developerUserId, now);

                foreach (var app in selectedPriority)
                {
                    app.LotteryResult = LotteryResultConstants.PriorityWon;
                    MoveToDepositPending(
                        app,
                        developerUserId,
                        now,
                        ReviewActionConstants.PriorityDirectApproval,
                        $"Duyệt ưu tiên + bàn giao căn {app.Apartment?.UnitName ?? ""} (không qua bốc thăm), chuyển sang bước thanh toán cọc Đợt 1 (10%).",
                        pendingNotify);
                    appsNeedingInstallments.Add(app.ApplicationId);
                }

                await ProjectUnitSeatHelper.SyncAvailableUnitsAsync(_context, projectId, _logger);
                project.UpdatedAt = now;
            }
            else
            {
                throw new InvalidOperationException($"Loại quyết định '{request.DecisionType}' không hợp lệ.");
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Lỗi xảy ra khi thực thi quyết định của CĐT cho dự án {ProjectId}", projectId);
            throw;
        }

        foreach (var appId in appsNeedingInstallments)
        {
            try
            {
                await _installmentService.FireTriggerEventAsync(
                    appId,
                    TriggerEventConstants.OnLotteryWon,
                    DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi sinh lịch thanh toán sau cấp căn cho App {AppId}", appId);
            }
        }

        foreach (var (applicantId, note) in pendingNotify)
        {
            try
            {
                await _notificationService.SendAsync(
                    applicantId,
                    "Đã chốt suất & cấp căn — vui lòng ký hợp đồng mua bán NOXH",
                    note,
                    NotificationTypeConstants.ContractPending);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi gửi thông báo CONTRACT_PENDING cho user {UserId}", applicantId);
            }
        }

        return true;
    }

    /// <summary>
    /// Gán căn AVAILABLE cho từng hồ sơ khi CĐT chốt / duyệt ưu tiên.
    /// </summary>
    private async Task AssignApartmentsForAppsAsync(
        Guid projectId,
        List<HousingApplication> apps,
        List<ApartmentAssignmentItemDto>? assignments,
        Guid changedBy,
        DateTime now)
    {
        if (assignments == null || assignments.Count == 0)
            throw new InvalidOperationException(
                "Phải chọn căn cụ thể cho từng hồ sơ được cấp (ApartmentAssignments).");

        if (assignments.Count != apps.Count)
            throw new InvalidOperationException(
                $"Số căn được chọn ({assignments.Count}) phải khớp số hồ sơ cấp ({apps.Count}).");

        var appIds = apps.Select(a => a.ApplicationId).ToHashSet();
        if (assignments.Any(x => !appIds.Contains(x.ApplicationId)))
            throw new InvalidOperationException("Có hồ sơ trong danh sách gán căn không thuộc đợt cấp này.");

        var aptIds = assignments.Select(x => x.ApartmentId).ToList();
        if (aptIds.Distinct().Count() != aptIds.Count)
            throw new InvalidOperationException("Không được gán cùng một căn cho nhiều hồ sơ.");

        var apartments = await _context.Apartments
            .Where(a => a.ProjectId == projectId && aptIds.Contains(a.Id))
            .ToListAsync();

        if (apartments.Count != aptIds.Count)
            throw new InvalidOperationException("Một hoặc nhiều căn không thuộc dự án này.");

        foreach (var item in assignments)
        {
            var app = apps.First(a => a.ApplicationId == item.ApplicationId);
            var apt = apartments.First(a => a.Id == item.ApartmentId);

            if (!string.Equals(apt.Status, ApartmentStatusConstants.Available, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Căn '{apt.UnitName}' không còn trống (Status={apt.Status}).");

            if (app.ApartmentId.HasValue)
                throw new InvalidOperationException($"Hồ sơ {app.FullName} đã được gán căn trước đó.");

            app.ApartmentId = apt.Id;
            app.Apartment = apt;
            apt.Status = ApartmentStatusConstants.Assigned;

            _context.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                HistoryId = Guid.NewGuid(),
                ApplicationId = app.ApplicationId,
                OldStatus = app.ApplicationStatus,
                NewStatus = app.ApplicationStatus,
                Action = ReviewActionConstants.AssignApartment,
                Note = $"Cấp căn khi chốt đợt: {apt.UnitName} {apt.Area}m² - {apt.Price:N0} VND",
                ChangedAt = now,
                ChangedBy = changedBy
            });
        }
    }

    private void MoveToDepositPending(
        HousingApplication app,
        Guid changedBy,
        DateTime now,
        string action,
        string note,
        List<(Guid ApplicantId, string Note)> pendingNotify)
    {
        var oldStatus = app.ApplicationStatus;
        app.ApplicationStatus = ApplicationStatusConstants.DepositPending;
        app.UpdatedAt = now;

        _context.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            HistoryId = Guid.NewGuid(),
            ApplicationId = app.ApplicationId,
            ChangedBy = changedBy,
            Action = action,
            OldStatus = oldStatus,
            NewStatus = ApplicationStatusConstants.DepositPending,
            Note = note,
            ChangedAt = now
        });

        pendingNotify.Add((
            app.ApplicantId,
            "Hồ sơ của bạn đã được chốt suất và cấp căn. Vui lòng thanh toán cọc Đợt 1 (10%) trên ứng dụng để tiến hành ký hợp đồng mua bán NOXH."));
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

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx &&
               (sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }

    private async Task<Guid?> ResolveDesiredApartmentTypeIdAsync(
        Guid? requestedTypeId,
        string? requestedTypeCode)
    {
        if (requestedTypeId.HasValue && requestedTypeId.Value != Guid.Empty)
            return requestedTypeId.Value;

        if (!string.IsNullOrWhiteSpace(requestedTypeCode))
        {
            var code = requestedTypeCode.Trim().ToUpperInvariant();
            var matchedType = await _context.ApartmentTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TypeCode == code);
            if (matchedType != null)
                return matchedType.Id;
        }

        return null;
    }

    public async Task<RHS.Application.DTOs.Eligibility.EligibilityResultDto> CheckEligibilityAsync(
        Guid applicantId,
        RHS.Application.DTOs.Eligibility.CheckEligibilityRequestDto request,
        CancellationToken ct = default)
    {
        User? user = null;
        List<UserHouseholdMember> savedMembers = new();

        if (request.UseProfileFallback && applicantId != Guid.Empty)
        {
            user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == applicantId, ct);

            savedMembers = await _context.UserHouseholdMembers
                .AsNoTracking()
                .Where(m => m.UserId == applicantId)
                .ToListAsync(ct);
        }

        var priorityGroup = !string.IsNullOrWhiteSpace(request.PriorityGroup)
            ? request.PriorityGroup
            : user?.PriorityGroup ?? PriorityGroupConstants.LowIncomeUrban;

        var maritalStatus = !string.IsNullOrWhiteSpace(request.MaritalStatus)
            ? request.MaritalStatus
            : user?.MaritalStatus ?? MaritalStatusConstants.Single;

        var monthlyIncome = request.MonthlyIncome ?? user?.MonthlyIncome;
        var spouseMonthlyIncome = request.SpouseMonthlyIncome ?? user?.SpouseMonthlyIncome;
        var housingStatus = !string.IsNullOrWhiteSpace(request.HousingStatus)
            ? request.HousingStatus
            : user?.HousingStatus ?? HousingStatusConstants.NoHouse;

        var membersCount = request.HouseholdMembers != null && request.HouseholdMembers.Count > 0
            ? request.HouseholdMembers.Count
            : savedMembers.Count;

        var isMarried = maritalStatus.Trim().ToUpperInvariant() == MaritalStatusConstants.Married;
        var totalPeople = 1 + (isMarried ? 1 : 0) + membersCount;

        decimal? avgArea = request.AverageHousingAreaPerPerson ?? user?.AverageHousingAreaPerPerson;
        if (request.TotalHousingArea.HasValue && request.TotalHousingArea.Value > 0)
        {
            avgArea = (decimal)Math.Round(request.TotalHousingArea.Value / totalPeople, 2);
        }

        return await _eligibilityEngine.AssessCriteriaAsync(
            priorityGroup:               priorityGroup,
            maritalStatus:               maritalStatus,
            monthlyIncome:               monthlyIncome,
            spouseMonthlyIncome:         spouseMonthlyIncome,
            housingStatus:               housingStatus,
            averageHousingAreaPerPerson: avgArea,
            totalMembersCount:           totalPeople,
            ct:                          ct);
    }
}
