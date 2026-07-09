using Microsoft.EntityFrameworkCore;
using RHS.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.HousingApplications;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Exceptions;
using System.Linq;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Service xử lý toàn bộ luồng xét duyệt hồ sơ nhà ở xã hội.
/// Maker-Checker (2-stage review):
///
///   Applicant: DRAFT → (submit) → SUBMITTED
///   VO (Maker): SUBMITTED → (assign) → UNDER_REVIEW
///              UNDER_REVIEW → (propose / request docs) → PROPOSED / NEED_MORE_DOCUMENTS
///              NEED_MORE_DOCUMENTS → (re-assign) → UNDER_REVIEW
///   WM (Checker): PROPOSED → (approve/reject/request docs) → APPROVED / REJECTED / NEED_MORE_DOCUMENTS
///                UNDER_REVIEW → (approve/reject/request docs) → APPROVED / REJECTED / NEED_MORE_DOCUMENTS
///
/// Chỉ WM mới được chốt duyệt/từ chối cuối cùng và trigger thay đổi AvailableUnits.
/// MỌI hành động đều ghi vào bảng ApplicationStatusHistory.
/// </summary>
public class ReviewService : IReviewService
{
    private readonly IHousingApplicationRepository _applicationRepo;
    private readonly IReviewHistoryRepository _historyRepo;
    private readonly IDocumentRepository _documentRepo;
    private readonly IHousingProjectRepository _projectRepo;
    private readonly INotificationService _notificationService;
    private readonly IPdfReceiptService _pdfReceiptService;
    private readonly AppDbContext _context;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(
        IHousingApplicationRepository applicationRepo,
        IReviewHistoryRepository historyRepo,
        IDocumentRepository documentRepo,
        IHousingProjectRepository projectRepo,
        INotificationService notificationService,
        IPdfReceiptService pdfReceiptService,
        AppDbContext context,
        ILogger<ReviewService> logger)
    {
        _applicationRepo    = applicationRepo;
        _historyRepo        = historyRepo;
        _documentRepo       = documentRepo;
        _projectRepo        = projectRepo;
        _notificationService = notificationService;
        _pdfReceiptService   = pdfReceiptService;
        _context            = context;
        _logger             = logger;
    }

    // ─────────────────────────────────────────────────────────────
    // Applicant: Submit hồ sơ (DRAFT → SUBMITTED)
    // ─────────────────────────────────────────────────────────────

    public async Task<ReviewResponseDto> SubmitApplicationAsync(
        Guid applicationId,
        Guid applicantId)
    {
        _logger.LogInformation(
            "Applicant {ApplicantId} submitting application {AppId}.",
            applicantId, applicationId);

        var application = await GetApplicationOrThrowAsync(applicationId);

        // Chỉ Applicant của hồ sơ mới được submit
        if (application.ApplicantId != applicantId)
            throw new UnauthorizedAccessException("Bạn không có quyền nộp hồ sơ này.");

        // State guard: chỉ DRAFT mới được submit
        ValidateTransition(
            current: application.ApplicationStatus,
            target: ApplicationStatusConstants.Submitted,
            allowedTargets: new[] { ApplicationStatusConstants.Submitted },
            validSources: new[] { ApplicationStatusConstants.Draft, ApplicationStatusConstants.NeedMoreDocuments });

        // Nghiệp vụ: phải có ít nhất 1 tài liệu trước khi nộp
        var documents = await _documentRepo.GetByApplicationIdAsync(applicationId);
        if (!documents.Any())
        {
            throw new ApplicationNotReadyToSubmitException(applicationId,
                "Hồ sơ chưa có tài liệu đính kèm. " +
                "Vui lòng upload ít nhất 1 loại giấy tờ chứng minh trước khi nộp.");
        }

        // Nghiệp vụ: CCCD phải là duy nhất trong cùng một dự án
        // Kiểm tra xem CitizenId của hồ sơ này đã tồn tại trong hồ sơ KHÁC của dự án chưa
        var citizenIdDuplicated = await _applicationRepo.CitizenIdExistsInProjectAsync(
            citizenId:            application.CitizenId,
            projectId:            application.ProjectId,
            excludeApplicationId: applicationId);

        if (citizenIdDuplicated)
        {
            _logger.LogWarning(
                "Submit blocked: CitizenId '{CitizenId}' already exists in project {ProjectId}. ApplicationId={AppId}.",
                application.CitizenId, application.ProjectId, applicationId);

            throw new DuplicateCitizenIdInProjectException(
                application.CitizenId,
                application.ProjectId);
        }

        var oldStatus = application.ApplicationStatus;
        var now = DateTime.UtcNow;

        // Cập nhật trạng thái và thời gian nộp thực tế
        application.ApplicationStatus = ApplicationStatusConstants.Submitted;
        application.SubmittedAt = now;
        application.UpdatedAt = now;

        await _applicationRepo.UpdateAsync(application);

        // Sinh biên nhận PDF và upload Cloudinary
        try
        {
            var project = await _projectRepo.GetByIdAsync(application.ProjectId);
            if (project != null)
            {
                var receiptUrl = await _pdfReceiptService.GenerateAndUploadReceiptAsync(application, project);
                application.ReceiptUrl = receiptUrl;
                await _applicationRepo.UpdateAsync(application);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi sinh biên nhận PDF cho hồ sơ {AppId}", applicationId);
            // Không ném lỗi để tránh làm nghẽn luồng nộp hồ sơ của người dân
        }

        // Ghi lịch sử
        await AppendHistoryAsync(
            applicationId: applicationId,
            changedBy: applicantId,
            action: ReviewActionConstants.Submit,
            oldStatus: oldStatus,
            newStatus: ApplicationStatusConstants.Submitted,
            note: null);

        _logger.LogInformation(
            "Application {AppId} submitted. Status: {Old} → {New}.",
            applicationId, oldStatus, ApplicationStatusConstants.Submitted);

        // Gửi thông báo cho Applicant
        await _notificationService.SendAsync(
            applicantId,
            "Hồ sơ đã được nộp thành công",
            $"Hồ sơ của bạn đã được nộp và đang chờ xét duyệt.",
            NotificationTypeConstants.ApplicationSubmitted);

        return BuildReviewResponse(applicationId, oldStatus,
            ApplicationStatusConstants.Submitted, ReviewActionConstants.Submit, now);
    }

    // ─────────────────────────────────────────────────────────────
    // Verification Officer: Nhận hồ sơ (SUBMITTED → UNDER_REVIEW)
    // ─────────────────────────────────────────────────────────────

    public async Task<ReviewResponseDto> AssignOfficerAsync(
        Guid applicationId,
        Guid officerId)
    {
        _logger.LogInformation(
            "Officer {OfficerId} assigning application {AppId}.",
            officerId, applicationId);

        var application = await GetApplicationOrThrowAsync(applicationId);

        // Validate: chỉ SUBMITTED mới được nhận
        ValidateTransition(
            current: application.ApplicationStatus,
            target: ApplicationStatusConstants.Reviewing,
            allowedTargets: new[] { ApplicationStatusConstants.Reviewing },
            validSources: new[]
            {
                ApplicationStatusConstants.Submitted,
                ApplicationStatusConstants.NeedMoreDocuments
            });

        var oldStatus = application.ApplicationStatus;
        var now = DateTime.UtcNow;

        application.ApplicationStatus = ApplicationStatusConstants.Reviewing;
        application.OfficerId = officerId;
        application.UpdatedAt = now;

        await _applicationRepo.UpdateAsync(application);

        await AppendHistoryAsync(
            applicationId: applicationId,
            changedBy: officerId,
            action: ReviewActionConstants.AssignOfficer,
            oldStatus: oldStatus,
            newStatus: ApplicationStatusConstants.Reviewing,
            note: null);

        _logger.LogInformation(
            "Application {AppId} assigned to developer {OfficerId}. Status: {Old} → {New}.",
            applicationId, officerId, oldStatus, ApplicationStatusConstants.Reviewing);

        return BuildReviewResponse(applicationId, oldStatus,
            ApplicationStatusConstants.Reviewing, ReviewActionConstants.AssignOfficer, now);
    }

    // ─────────────────────────────────────────────────────────────
    // Housing Developer (CĐT) Review
    // ─────────────────────────────────────────────────────────────

    public async Task<ReviewResponseDto> HousingDeveloperReviewAsync(
        Guid applicationId,
        Guid developerId,
        HousingDeveloperReviewRequestDto request)
    {
        _logger.LogInformation(
            "CĐT {DeveloperId} reviewing application {AppId}. Action={Action}.",
            developerId, applicationId, request.Action);

        // Validate action value
        var (action, targetStatus) = ResolveDeveloperAction(request.Action);

        // Note bắt buộc khi Reject hoặc Request More Documents
        if ((action == ReviewActionConstants.Reject || action == ReviewActionConstants.RequestMoreDocuments)
            && string.IsNullOrWhiteSpace(request.Note))
        {
            throw new ArgumentException(
                "Ghi chú (Note) là bắt buộc khi thực hiện REJECT hoặc REQUEST_MORE_DOCUMENTS.");
        }

        var application = await GetApplicationOrThrowAsync(applicationId);

        // Validate: CĐT phải được giao hồ sơ này
        if (application.OfficerId.HasValue && application.OfficerId != developerId)
        {
            throw new UnauthorizedAccessException(
                "Bạn không phải cán bộ được giao thẩm định hồ sơ này.");
        }

        // Validate state machine dựa trên HousingDeveloperTransitions
        ValidateDeveloperTransition(application.ApplicationStatus, targetStatus);

        var oldStatus = application.ApplicationStatus;
        var now = DateTime.UtcNow;

        application.ApplicationStatus = targetStatus;
        application.UpdatedAt = now;

        await _applicationRepo.UpdateAsync(application);

        await AppendHistoryAsync(
            applicationId: applicationId,
            changedBy: developerId,
            action: action,
            oldStatus: oldStatus,
            newStatus: targetStatus,
            note: request.Note?.Trim());

        _logger.LogInformation(
            "CĐT {DeveloperId} reviewed application {AppId}. Status: {Old} → {New}.",
            developerId, applicationId, oldStatus, targetStatus);

        // Gửi thông báo cho SXD hoặc Applicant
        await SendDeveloperProposalNotificationAsync(application, request.Note);

        return BuildReviewResponse(applicationId, oldStatus, targetStatus, action, now);
    }

    // ─────────────────────────────────────────────────────────────
    // Department Of Construction (SXD) Review
    // ─────────────────────────────────────────────────────────────

    public async Task<ReviewResponseDto> DepartmentOfConstructionReviewAsync(
        Guid applicationId,
        Guid sxdUserId,
        DepartmentOfConstructionReviewRequestDto request)
    {
        _logger.LogInformation(
            "SXD {SxdUserId} reviewing application {AppId}. Action={Action}.",
            sxdUserId, applicationId, request.Action);

        // Validate action value
        var (action, targetStatus) = ResolveDepartmentOfConstructionAction(request.Action);

        // Note bắt buộc khi Reject hoặc Request More Documents
        if (action is ReviewActionConstants.Reject
                   or ReviewActionConstants.RequestMoreDocuments
            && string.IsNullOrWhiteSpace(request.Note))
        {
            throw new ArgumentException(
                "Ghi chú (Note) là bắt buộc khi thực hiện REJECT hoặc REQUEST_MORE_DOCUMENTS.");
        }

        var application = await GetApplicationOrThrowAsync(applicationId);

        // Validate state machine dựa trên DepartmentOfConstructionTransitions
        ValidateDepartmentOfConstructionTransition(application.ApplicationStatus, targetStatus);

        var oldStatus = application.ApplicationStatus;
        var now = DateTime.UtcNow;

        using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Chỉ SXD mới trigger thay đổi AvailableUnits khi phê duyệt
            if (targetStatus == ApplicationStatusConstants.Approved)
            {
                var project = await _context.HousingProjects.FirstOrDefaultAsync(p => p.Id == application.ProjectId);
                if (project == null)
                {
                    throw new InvalidOperationException("Không tìm thấy dự án tương ứng với hồ sơ.");
                }
                if (project.AvailableUnits <= 0)
                {
                    throw new InvalidOperationException("Dự án đã hết căn hộ trống để giữ chỗ.");
                }
                project.AvailableUnits -= 1;
                project.UpdatedAt = now;
                _context.HousingProjects.Update(project);
            }

            application.ApplicationStatus = targetStatus;
            application.UpdatedAt = now;

            if (targetStatus is ApplicationStatusConstants.Approved
                             or ApplicationStatusConstants.Rejected)
            {
                application.FinalDecisionDate = now;
            }

            await _applicationRepo.UpdateAsync(application);

            await AppendHistoryAsync(
                applicationId: applicationId,
                changedBy: sxdUserId,
                action: action,
                oldStatus: oldStatus,
                newStatus: targetStatus,
                note: request.Note?.Trim());

            await dbTransaction.CommitAsync();
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            throw;
        }

        _logger.LogInformation(
            "SXD {SxdUserId} finalized application {AppId}. Status: {Old} → {New}.",
            sxdUserId, applicationId, oldStatus, targetStatus);

        // Gửi thông báo cho Applicant sau khi transaction commit thành công
        await SendReviewNotificationAsync(application.ApplicantId, targetStatus, request.Note);

        return BuildReviewResponse(applicationId, oldStatus, targetStatus, action, now);
    }

    // ─────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────

    /// <summary>Tải hồ sơ và ném ApplicationNotFoundException nếu không tìm thấy.</summary>
    private async Task<HousingApplication> GetApplicationOrThrowAsync(Guid applicationId)
    {
        var application = await _applicationRepo.GetByIdWithDetailsAsync(applicationId);
        if (application is null)
            throw new ApplicationNotFoundException(applicationId);
        return application;
    }

    /// <summary>
    /// Validate chuyển trạng thái tổng quát.
    /// Ném InvalidApplicationStatusTransitionException nếu vi phạm.
    /// </summary>
    private static void ValidateTransition(
        string current,
        string target,
        string[] allowedTargets,
        string[] validSources)
    {
        if (!validSources.Contains(current))
        {
            throw new InvalidApplicationStatusTransitionException(current, target);
        }

        if (!allowedTargets.Contains(target))
        {
            throw new InvalidApplicationStatusTransitionException(current, target);
        }
    }
    /// <summary>Validate chuyển trạng thái theo state machine của CĐT.</summary>
    private static void ValidateDeveloperTransition(string currentStatus, string targetStatus)
    {
        if (!ApplicationStatusConstants.HousingDeveloperTransitions.TryGetValue(
                currentStatus, out var allowed)
            || !allowed.Contains(targetStatus))
        {
            throw new InvalidApplicationStatusTransitionException(
                currentStatus, targetStatus, RoleConstants.HousingDeveloper);
        }
    }

    /// <summary>Validate chuyển trạng thái theo state machine của SXD.</summary>
    private static void ValidateDepartmentOfConstructionTransition(string currentStatus, string targetStatus)
    {
        if (!ApplicationStatusConstants.DepartmentOfConstructionTransitions.TryGetValue(
                currentStatus, out var allowed)
            || !allowed.Contains(targetStatus))
        {
            throw new InvalidApplicationStatusTransitionException(
                currentStatus, targetStatus, RoleConstants.DepartmentOfConstruction);
        }
    }

    /// <summary>
    /// Resolve action của CĐT.
    /// </summary>
    private static (string action, string targetStatus) ResolveDeveloperAction(string actionInput)
    {
        return actionInput.ToUpperInvariant() switch
        {
            "PROPOSE"                => (ReviewActionConstants.Propose,              ApplicationStatusConstants.PendingSxdReview),
            "REQUEST_MORE_DOCUMENTS" => (ReviewActionConstants.RequestMoreDocuments, ApplicationStatusConstants.NeedMoreDocuments),
            "REJECT"                 => (ReviewActionConstants.Reject,               ApplicationStatusConstants.Rejected),
            _ => throw new ArgumentException(
                $"Hành động '{actionInput}' không hợp lệ cho Housing Developer. " +
                "Giá trị hợp lệ: PROPOSE, REQUEST_MORE_DOCUMENTS, REJECT.")
        };
    }

    /// <summary>
    /// Resolve action của SXD.
    /// </summary>
    private static (string action, string targetStatus) ResolveDepartmentOfConstructionAction(string actionInput)
    {
        return actionInput.ToUpperInvariant() switch
        {
            "APPROVE"                => (ReviewActionConstants.Approve,              ApplicationStatusConstants.Approved),
            "REJECT"                 => (ReviewActionConstants.Reject,               ApplicationStatusConstants.Rejected),
            "REQUEST_MORE_DOCUMENTS" => (ReviewActionConstants.RequestMoreDocuments, ApplicationStatusConstants.NeedMoreDocuments),
            _ => throw new ArgumentException(
                $"Hành động '{actionInput}' không hợp lệ cho Department Of Construction. " +
                "Giá trị hợp lệ: APPROVE, REJECT, REQUEST_MORE_DOCUMENTS.")
        };
    }

    /// <summary>
    /// Ghi một bản ghi lịch sử xét duyệt vào DB.
    /// Được gọi sau mọi thao tác thay đổi trạng thái hồ sơ.
    /// </summary>
    private async Task AppendHistoryAsync(
        Guid applicationId,
        Guid changedBy,
        string action,
        string oldStatus,
        string newStatus,
        string? note)
    {
        var history = new ApplicationStatusHistory
        {
            HistoryId     = Guid.NewGuid(),
            ApplicationId = applicationId,
            ChangedBy     = changedBy,
            Action        = action,
            OldStatus     = oldStatus,
            NewStatus     = newStatus,
            Note          = note,
            ChangedAt     = DateTime.UtcNow
        };

        await _historyRepo.CreateAsync(history);

        _logger.LogDebug(
            "ReviewHistory recorded: App={AppId}, By={ChangedBy}, " +
            "Action={Action}, {Old}→{New}.",
            applicationId, changedBy, action, oldStatus, newStatus);
    }

    /// <summary>Build ReviewResponseDto sau khi thực hiện xét duyệt thành công.</summary>
    private static ReviewResponseDto BuildReviewResponse(
        Guid applicationId,
        string oldStatus,
        string newStatus,
        string action,
        DateTime reviewedAt)
    {
        var actionLabel = action switch
        {
            ReviewActionConstants.Submit               => "Nộp hồ sơ",
            ReviewActionConstants.AssignOfficer        => "Nhận hồ sơ thẩm định",
            ReviewActionConstants.Propose              => "Đề xuất phê duyệt",
            ReviewActionConstants.Approve              => "Phê duyệt hồ sơ",
            ReviewActionConstants.Reject               => "Từ chối hồ sơ",
            ReviewActionConstants.RequestMoreDocuments => "Yêu cầu bổ sung giấy tờ",
            _ => action
        };

        return new ReviewResponseDto
        {
            ApplicationId = applicationId,
            OldStatus     = oldStatus,
            NewStatus     = newStatus,
            Action        = action,
            ReviewedAt    = reviewedAt,
            Message       = $"{actionLabel} thành công. Trạng thái: {oldStatus} → {newStatus}."
        };
    }

    /// <summary>
    /// Gửi thông báo khi CĐT đề xuất phê duyệt.
    /// </summary>
    private async Task SendDeveloperProposalNotificationAsync(HousingApplication application, string? note)
    {
        // Thông báo cho Applicant
        await _notificationService.SendAsync(
            application.ApplicantId,
            "Hồ sơ đã được gửi lên Sở Xây Dựng",
            "Hồ sơ của bạn đã được CĐT thẩm định và đề xuất gửi lên Sở Xây Dựng phê duyệt." +
            (string.IsNullOrWhiteSpace(note) ? "" : $" Ghi chú: {note}"),
            "APPLICATION_PENDING_SXD_REVIEW");
    }

    /// <summary>
    /// Gửi thông báo in-app cho Applicant dựa trên quyết định cuối cùng.
    /// </summary>
    private async Task SendReviewNotificationAsync(
        Guid applicantId, string targetStatus, string? note)
    {
        var (title, content, notifType) = targetStatus switch
        {
            ApplicationStatusConstants.Approved => (
                "Hồ sơ đã được phê duyệt",
                "Hồ sơ của bạn đã được Sở Xây Dựng phê duyệt. Vui lòng thanh toán đặt cọc trong vòng 24 giờ để giữ suất.",
                NotificationTypeConstants.ApplicationApproved),

            ApplicationStatusConstants.Rejected => (
                "Hồ sơ bị từ chối",
                $"Hồ sơ của bạn đã bị từ chối.{(string.IsNullOrWhiteSpace(note) ? "" : $" Lý do: {note}")}",
                NotificationTypeConstants.ApplicationRejected),

            ApplicationStatusConstants.NeedMoreDocuments => (
                "Yêu cầu bổ sung giấy tờ",
                $"Hồ sơ của bạn cần bổ sung giấy tờ.{(string.IsNullOrWhiteSpace(note) ? "" : $" Chi tiết: {note}")}",
                NotificationTypeConstants.ApplicationNeedMoreDocs),

            _ => (null!, null!, null!)
        };

        if (title != null)
        {
            await _notificationService.SendAsync(applicantId, title, content, notifType);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Task #11: Applicant tự hủy hồ sơ (stub — full implementation in Commit 9)
    // ─────────────────────────────────────────────────────────────

    public async Task<ReviewResponseDto> CancelApplicationAsync(
        Guid applicationId,
        Guid applicantId,
        CancelApplicationRequestDto request)
    {
        throw new NotImplementedException("CancelApplicationAsync will be implemented in Commit 9.");
    }

    // ─────────────────────────────────────────────────────────────
    // Task #7: CĐT gửi danh sách lên SXD (stub — full implementation in Commit 5)
    // ─────────────────────────────────────────────────────────────

    public async Task<List<ReviewResponseDto>> SubmitToDepartmentAsync(
        Guid developerId,
        SubmitToDepartmentRequestDto request)
    {
        throw new NotImplementedException("SubmitToDepartmentAsync will be implemented in Commit 5.");
    }
}
