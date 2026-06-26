using Microsoft.EntityFrameworkCore;
using RHS.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.HousingApplications;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Exceptions;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Service xử lý toàn bộ luồng xét duyệt hồ sơ nhà ở xã hội.
/// Áp dụng state machine dựa trên ApplicationStatusConstants:
///
///   Applicant: DRAFT → (submit) → SUBMITTED
///   VO:        SUBMITTED → (assign) → UNDER_REVIEW
///              UNDER_REVIEW → (approve/reject) → APPROVED / REJECTED
///              NEED_MORE_DOCUMENTS → (re-assign) → UNDER_REVIEW
///   WM:        UNDER_REVIEW → (approve/reject/request) → APPROVED / REJECTED / NEED_MORE_DOCUMENTS
///
/// MỌI hành động đều ghi vào bảng ApplicationStatusHistory.
/// </summary>
public class ReviewService : IReviewService
{
    private readonly IHousingApplicationRepository _applicationRepo;
    private readonly IReviewHistoryRepository _historyRepo;
    private readonly IDocumentRepository _documentRepo;
    private readonly IHousingProjectRepository _projectRepo;
    private readonly INotificationService _notificationService;
    private readonly AppDbContext _context;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(
        IHousingApplicationRepository applicationRepo,
        IReviewHistoryRepository historyRepo,
        IDocumentRepository documentRepo,
        IHousingProjectRepository projectRepo,
        INotificationService notificationService,
        AppDbContext context,
        ILogger<ReviewService> logger)
    {
        _applicationRepo    = applicationRepo;
        _historyRepo        = historyRepo;
        _documentRepo       = documentRepo;
        _projectRepo        = projectRepo;
        _notificationService = notificationService;
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
            validSources: new[] { ApplicationStatusConstants.Draft });

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
            target: ApplicationStatusConstants.UnderReview,
            allowedTargets: new[] { ApplicationStatusConstants.UnderReview },
            validSources: new[]
            {
                ApplicationStatusConstants.Submitted,
                ApplicationStatusConstants.NeedMoreDocuments
            });

        var oldStatus = application.ApplicationStatus;
        var now = DateTime.UtcNow;

        application.ApplicationStatus = ApplicationStatusConstants.UnderReview;
        application.OfficerId = officerId;
        application.UpdatedAt = now;

        await _applicationRepo.UpdateAsync(application);

        await AppendHistoryAsync(
            applicationId: applicationId,
            changedBy: officerId,
            action: ReviewActionConstants.AssignOfficer,
            oldStatus: oldStatus,
            newStatus: ApplicationStatusConstants.UnderReview,
            note: null);

        _logger.LogInformation(
            "Application {AppId} assigned to officer {OfficerId}. Status: {Old} → {New}.",
            applicationId, officerId, oldStatus, ApplicationStatusConstants.UnderReview);

        return BuildReviewResponse(applicationId, oldStatus,
            ApplicationStatusConstants.UnderReview, ReviewActionConstants.AssignOfficer, now);
    }

    // ─────────────────────────────────────────────────────────────
    // Verification Officer: Xét duyệt (UNDER_REVIEW → APPROVED/REJECTED)
    // ─────────────────────────────────────────────────────────────

    public async Task<ReviewResponseDto> VerificationOfficerReviewAsync(
        Guid applicationId,
        Guid officerId,
        VerificationOfficerReviewRequestDto request)
    {
        _logger.LogInformation(
            "VO {OfficerId} reviewing application {AppId}. Action={Action}.",
            officerId, applicationId, request.Action);

        // Validate action value
        var (action, targetStatus) = ResolveVoAction(request.Action);

        // Note bắt buộc khi Reject hoặc Request More Documents
        if (action is ReviewActionConstants.Reject
                   or ReviewActionConstants.RequestMoreDocuments
            && string.IsNullOrWhiteSpace(request.Note))
        {
            throw new ArgumentException(
                "Ghi chú (Note) là bắt buộc khi thực hiện REJECT hoặc REQUEST_MORE_DOCUMENTS.");
        }

        var application = await GetApplicationOrThrowAsync(applicationId);

        // Validate: VO phải được giao hồ sơ này
        if (application.OfficerId.HasValue && application.OfficerId != officerId)
        {
            throw new UnauthorizedAccessException(
                "Bạn không phải cán bộ được giao thẩm định hồ sơ này.");
        }

        // Validate state machine dựa trên VerificationOfficerTransitions
        ValidateVoTransition(application.ApplicationStatus, targetStatus);

        var oldStatus = application.ApplicationStatus;
        var now = DateTime.UtcNow;

        using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (targetStatus == ApplicationStatusConstants.Approved)
            {
                var project = await _context.HousingProjects.FirstOrDefaultAsync(p => p.Id == application.ProjectId);
                if (project == null)
                {
                    throw new InvalidOperationException("Khong tim thay du an tuong ung voi ho so.");
                }
                if (project.AvailableUnits <= 0)
                {
                    throw new InvalidOperationException("Du an da het can ho trong de giu cho.");
                }
                project.AvailableUnits -= 1;
                project.UpdatedAt = now;
                _context.HousingProjects.Update(project);
            }

            application.ApplicationStatus = targetStatus;
            application.UpdatedAt = now;

            // Neu la quyet dinh cuoi thi ghi FinalDecisionDate
            if (targetStatus is ApplicationStatusConstants.Approved
                             or ApplicationStatusConstants.Rejected)
            {
                application.FinalDecisionDate = now;
            }

            await _applicationRepo.UpdateAsync(application);

            await AppendHistoryAsync(
                applicationId: applicationId,
                changedBy: officerId,
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
            "VO {OfficerId} reviewed application {AppId}. Status: {Old} " + (char)0x2192 + " {New}.",
            officerId, applicationId, oldStatus, targetStatus);

        // Gửi thông báo cho Applicant sau khi transaction commit thành công
        await SendReviewNotificationAsync(application.ApplicantId, targetStatus, request.Note);

        return BuildReviewResponse(applicationId, oldStatus, targetStatus, action, now);
    }

    // ─────────────────────────────────────────────────────────────
    // Ward Manager: Xét duyệt (UNDER_REVIEW → APPROVED/REJECTED/NEED_MORE_DOCUMENTS)
    // ─────────────────────────────────────────────────────────────

    public async Task<ReviewResponseDto> WardManagerReviewAsync(
        Guid applicationId,
        Guid managerId,
        WardManagerReviewRequestDto request)
    {
        _logger.LogInformation(
            "WM {ManagerId} reviewing application {AppId}. Action={Action}.",
            managerId, applicationId, request.Action);

        // Validate action value
        var (action, targetStatus) = ResolveWmAction(request.Action);

        // Note bắt buộc khi Reject hoặc Request More Documents
        if (action is ReviewActionConstants.Reject
                   or ReviewActionConstants.RequestMoreDocuments
            && string.IsNullOrWhiteSpace(request.Note))
        {
            throw new ArgumentException(
                "Ghi chú (Note) là bắt buộc khi thực hiện REJECT hoặc REQUEST_MORE_DOCUMENTS.");
        }

        var application = await GetApplicationOrThrowAsync(applicationId);

        // Validate state machine dựa trên WardManagerTransitions
        ValidateWmTransition(application.ApplicationStatus, targetStatus);

        var oldStatus = application.ApplicationStatus;
        var now = DateTime.UtcNow;

        using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (targetStatus == ApplicationStatusConstants.Approved)
            {
                var project = await _context.HousingProjects.FirstOrDefaultAsync(p => p.Id == application.ProjectId);
                if (project == null)
                {
                    throw new InvalidOperationException("Khong tim thay du an tuong ung voi ho so.");
                }
                if (project.AvailableUnits <= 0)
                {
                    throw new InvalidOperationException("Du an da het can ho trong de giu cho.");
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
                changedBy: managerId,
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
            "WM {ManagerId} reviewed application {AppId}. Status: {Old} " + (char)0x2192 + " {New}.",
            managerId, applicationId, oldStatus, targetStatus);

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

    /// <summary>Validate chuyển trạng thái theo state machine của VO.</summary>
    private static void ValidateVoTransition(string currentStatus, string targetStatus)
    {
        if (!ApplicationStatusConstants.VerificationOfficerTransitions.TryGetValue(
                currentStatus, out var allowed)
            || !allowed.Contains(targetStatus))
        {
            throw new InvalidApplicationStatusTransitionException(
                currentStatus, targetStatus, RoleConstants.VerificationOfficer);
        }
    }

    /// <summary>Validate chuyển trạng thái theo state machine của WM.</summary>
    private static void ValidateWmTransition(string currentStatus, string targetStatus)
    {
        if (!ApplicationStatusConstants.WardManagerTransitions.TryGetValue(
                currentStatus, out var allowed)
            || !allowed.Contains(targetStatus))
        {
            throw new InvalidApplicationStatusTransitionException(
                currentStatus, targetStatus, RoleConstants.WardManager);
        }
    }

    /// <summary>
    /// Resolve action string của VO thành (ReviewAction, TargetStatus).
    /// Ném ArgumentException nếu action không hợp lệ.
    /// </summary>
    private static (string action, string targetStatus) ResolveVoAction(string actionInput)
    {
        return actionInput.ToUpperInvariant() switch
        {
            "APPROVE"                => (ReviewActionConstants.Approve,              ApplicationStatusConstants.Approved),
            "REJECT"                 => (ReviewActionConstants.Reject,               ApplicationStatusConstants.Rejected),
            "REQUEST_MORE_DOCUMENTS" => (ReviewActionConstants.RequestMoreDocuments, ApplicationStatusConstants.NeedMoreDocuments),
            _ => throw new ArgumentException(
                $"Hành động '{actionInput}' không hợp lệ cho Verification Officer. " +
                "Giá trị hợp lệ: APPROVE, REJECT, REQUEST_MORE_DOCUMENTS.")
        };
    }

    /// <summary>
    /// Resolve action string của WM thành (ReviewAction, TargetStatus).
    /// Ném ArgumentException nếu action không hợp lệ.
    /// </summary>
    private static (string action, string targetStatus) ResolveWmAction(string actionInput)
    {
        return actionInput.ToUpperInvariant() switch
        {
            "APPROVE"                => (ReviewActionConstants.Approve,               ApplicationStatusConstants.Approved),
            "REJECT"                 => (ReviewActionConstants.Reject,                ApplicationStatusConstants.Rejected),
            "REQUEST_MORE_DOCUMENTS" => (ReviewActionConstants.RequestMoreDocuments,  ApplicationStatusConstants.NeedMoreDocuments),
            _ => throw new ArgumentException(
                $"Hành động '{actionInput}' không hợp lệ cho Ward Manager. " +
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
    /// Gửi thông báo in-app cho Applicant dựa trên trạng thái mới.
    /// Được gọi SAU khi transaction commit thành công.
    /// </summary>
    private async Task SendReviewNotificationAsync(
        Guid applicantId, string targetStatus, string? note)
    {
        var (title, content, notifType) = targetStatus switch
        {
            ApplicationStatusConstants.Approved => (
                "Hồ sơ đã được phê duyệt",
                "Hồ sơ của bạn đã được phê duyệt. Vui lòng thanh toán đặt cọc trong vòng 24 giờ để giữ suất.",
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
}
