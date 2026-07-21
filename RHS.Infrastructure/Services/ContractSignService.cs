using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.ContractSign;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Triển khai IContractSignService — giả lập ký số hợp đồng nguyên tắc.
/// Người dân bấm "Đồng ý điều khoản" trên App → hệ thống ghi nhận:
/// 1. PrincipleAgreement: IsSigned=true, SignedAt, SignedIpAddress
/// 2. HousingApplication: ApplicationStatus → CONTRACT_SIGNED
/// 3. ApplicationStatusHistory: action=CONTRACT_SIGNED
/// 4. Notification cho Applicant
/// </summary>
public class ContractSignService : IContractSignService
{
    private readonly IHousingApplicationRepository _applicationRepo;
    private readonly IPrincipleAgreementRepository _agreementRepo;
    private readonly INotificationService _notificationService;
    private readonly AppDbContext _context;
    private readonly ILogger<ContractSignService> _logger;

    public ContractSignService(
        IHousingApplicationRepository applicationRepo,
        IPrincipleAgreementRepository agreementRepo,
        INotificationService notificationService,
        AppDbContext context,
        ILogger<ContractSignService> logger)
    {
        _applicationRepo     = applicationRepo;
        _agreementRepo       = agreementRepo;
        _notificationService = notificationService;
        _context             = context;
        _logger              = logger;
    }

    /// <inheritdoc/>
    public async Task<ContractSignResponseDto> SignContractAsync(
        Guid applicantId, Guid applicationId, string? ipAddress)
    {
        _logger.LogInformation(
            "User {UserId} signing contract for Application {AppId}.",
            applicantId, applicationId);

        // ── 1. Load & validate Application ──────────────────────────────────
        var application = await _applicationRepo.GetByIdWithDetailsAsync(applicationId);
        if (application == null)
        {
            return new ContractSignResponseDto
            {
                Success = false,
                Message = $"Không tìm thấy hồ sơ với ID: {applicationId}"
            };
        }

        // Chỉ chủ hồ sơ mới được ký
        if (application.ApplicantId != applicantId)
        {
            return new ContractSignResponseDto
            {
                Success = false,
                Message = "Bạn không phải chủ hồ sơ này."
            };
        }

        // Chỉ cho ký khi hồ sơ ở trạng thái DEPOSIT_PAID hoặc FULLY_PAID
        var allowedStatuses = new[]
        {
            ApplicationStatusConstants.DepositPaid,
            ApplicationStatusConstants.FullyPaid
        };

        if (!allowedStatuses.Contains(application.ApplicationStatus))
        {
            // Nếu đã ký rồi (CONTRACT_SIGNED) → trả OK (idempotent)
            if (application.ApplicationStatus == ApplicationStatusConstants.ContractSigned)
            {
                var existingAgreement = await _agreementRepo.GetByApplicationIdAsync(applicationId);
                return new ContractSignResponseDto
                {
                    Success  = true,
                    Message  = "Hợp đồng đã được ký trước đó.",
                    SignedAt = existingAgreement?.SignedAt
                };
            }

            return new ContractSignResponseDto
            {
                Success = false,
                Message = $"Hồ sơ chưa đủ điều kiện ký hợp đồng. Trạng thái hiện tại: {application.ApplicationStatus}"
            };
        }

        // ── 2. Load PrincipleAgreement ──────────────────────────────────────
        var agreement = await _agreementRepo.GetByApplicationIdAsync(applicationId);
        if (agreement == null)
        {
            return new ContractSignResponseDto
            {
                Success = false,
                Message = "Hợp đồng nguyên tắc chưa được tạo. Vui lòng liên hệ hỗ trợ."
            };
        }

        // Idempotency guard: nếu đã ký rồi → trả OK
        if (agreement.IsSigned)
        {
            _logger.LogInformation(
                "Application {AppId} contract already signed at {SignedAt}. Skipping.",
                applicationId, agreement.SignedAt);

            return new ContractSignResponseDto
            {
                Success  = true,
                Message  = "Hợp đồng đã được ký trước đó.",
                SignedAt = agreement.SignedAt
            };
        }

        // ── 3. Thực hiện ký trong 1 DB transaction ─────────────────────────
        var signedAt = DateTime.UtcNow;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 3a. Update PrincipleAgreement
            agreement.IsSigned       = true;
            agreement.SignedAt       = signedAt;
            agreement.SignedIpAddress = ipAddress;
            _context.PrincipleAgreements.Update(agreement);

            // 3b. Update HousingApplication status → CONTRACT_SIGNED
            var oldStatus = application.ApplicationStatus;
            application.ApplicationStatus = ApplicationStatusConstants.ContractSigned;
            application.UpdatedAt = DateTime.UtcNow;
            await _applicationRepo.UpdateAsync(application);

            // 3c. Ghi ApplicationStatusHistory
            var history = new ApplicationStatusHistory
            {
                HistoryId     = Guid.NewGuid(),
                ApplicationId = applicationId,
                ChangedBy     = applicantId,
                Action        = ReviewActionConstants.ContractSigned,
                OldStatus     = oldStatus,
                NewStatus     = ApplicationStatusConstants.ContractSigned,
                Note          = $"Người dân đồng ý điều khoản hợp đồng nguyên tắc. IP: {ipAddress ?? "N/A"}",
                ChangedAt     = signedAt
            };
            await _context.ApplicationStatusHistories.AddAsync(history);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "Contract signed: AppId={AppId}, Status={Old}→{New}, SignedAt={SignedAt}.",
                applicationId, oldStatus, ApplicationStatusConstants.ContractSigned, signedAt);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex,
                "Contract signing failed for Application {AppId}.", applicationId);
            throw;
        }

        // ── 4. Gửi notification (sau commit) ────────────────────────────────
        await _notificationService.SendAsync(
            applicantId,
            "Ký hợp đồng thành công",
            "Bạn đã đồng ý điều khoản hợp đồng nguyên tắc. Hợp đồng đã được ghi nhận trạng thái đã ký.",
            NotificationTypeConstants.ContractSigned);

        return new ContractSignResponseDto
        {
            Success  = true,
            Message  = "Ký hợp đồng nguyên tắc thành công.",
            SignedAt = signedAt
        };
    }

    /// <inheritdoc/>
    public async Task<ContractSignStatusDto?> GetSignStatusAsync(Guid applicationId)
    {
        var agreement = await _agreementRepo.GetByApplicationIdAsync(applicationId);
        if (agreement == null)
            return null;

        var application = await _applicationRepo.GetByIdWithDetailsAsync(applicationId);

        return new ContractSignStatusDto
        {
            ApplicationId     = applicationId,
            IsSigned          = agreement.IsSigned,
            SignedAt          = agreement.SignedAt,
            PdfUrl            = agreement.PdfUrl,
            ApplicationStatus = application?.ApplicationStatus ?? string.Empty
        };
    }
}
