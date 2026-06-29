using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.Payment;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Triển khai IPaymentService – điều phối toàn bộ nghiệp vụ thanh toán đặt cọc:
/// 1. Tạo giao dịch Pending → lấy URL VNPay
/// 2. Xử lý callback → xác minh chữ ký → cập nhật trạng thái DB
/// 3. Nếu thanh toán thành công → sinh SlotCode + PDF hợp đồng + PrincipleAgreement
/// 4. Cung cấp API tra cứu lịch sử
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IVnPayService _vnPayService;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IHousingApplicationRepository _applicationRepo;
    private readonly IHousingProjectRepository _projectRepo;
    private readonly IPdfContractService _pdfContractService;
    private readonly IPrincipleAgreementRepository _agreementRepo;
    private readonly IReviewHistoryRepository _historyRepo;
    private readonly INotificationService _notificationService;
    private readonly AppDbContext _context;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IVnPayService vnPayService,
        IPaymentRepository paymentRepository,
        IHousingApplicationRepository applicationRepo,
        IHousingProjectRepository projectRepo,
        IPdfContractService pdfContractService,
        IPrincipleAgreementRepository agreementRepo,
        IReviewHistoryRepository historyRepo,
        INotificationService notificationService,
        AppDbContext context,
        ILogger<PaymentService> logger)
    {
        _vnPayService        = vnPayService;
        _paymentRepository   = paymentRepository;
        _applicationRepo     = applicationRepo;
        _projectRepo         = projectRepo;
        _pdfContractService  = pdfContractService;
        _agreementRepo       = agreementRepo;
        _historyRepo         = historyRepo;
        _notificationService = notificationService;
        _context             = context;
        _logger              = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1. Tạo thanh toán đặt cọc
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc/>
    public async Task<PaymentResponseDto> CreatePaymentAsync(
        Guid userId,
        CreatePaymentDto dto,
        HttpContext httpContext)
    {
        _logger.LogInformation(
            "User {UserId} creating deposit payment for Application {AppId}.",
            userId, dto.ApplicationId);

        // ── 1. Load & validate Application ──────────────────────────────────
        var application = await _applicationRepo.GetByIdWithDetailsAsync(dto.ApplicationId);
        if (application == null)
        {
            return new PaymentResponseDto
            {
                Success = false,
                Message = $"Không tìm thấy hồ sơ với ID: {dto.ApplicationId}"
            };
        }

        // Chỉ chủ hồ sơ mới được thanh toán
        if (application.ApplicantId != userId)
        {
            return new PaymentResponseDto
            {
                Success = false,
                Message = "Bạn không phải chủ hồ sơ này."
            };
        }

        // Chỉ hồ sơ APPROVED mới được thanh toán
        if (application.ApplicationStatus != ApplicationStatusConstants.Approved)
        {
            return new PaymentResponseDto
            {
                Success = false,
                Message = $"Hồ sơ chưa được phê duyệt. Trạng thái hiện tại: {application.ApplicationStatus}"
            };
        }

        // Kiểm tra không có payment Pending hoặc Success nào cho application này
        var existingPayment = await _context.Payments
            .AnyAsync(p => p.ApplicationId == dto.ApplicationId
                        && (p.Status == "Pending" || p.Status == "Success"));

        if (existingPayment)
        {
            return new PaymentResponseDto
            {
                Success = false,
                Message = "Hồ sơ này đã có giao dịch thanh toán đang chờ hoặc đã thành công."
            };
        }

        // ── 2. Lấy DepositAmount từ HousingProject ─────────────────────────
        var project = await _projectRepo.GetByIdAsync(application.ProjectId);
        if (project == null)
        {
            return new PaymentResponseDto
            {
                Success = false,
                Message = "Không tìm thấy dự án liên kết với hồ sơ."
            };
        }

        var depositAmount = project.DepositAmount;
        if (depositAmount <= 0)
        {
            return new PaymentResponseDto
            {
                Success = false,
                Message = "Dự án chưa cấu hình số tiền đặt cọc."
            };
        }

        // ── 3. Tạo mã đơn hàng + lưu Payment ──────────────────────────────
        var orderId = GenerateOrderId();
        var orderInfo = dto.OrderInfo
            ?? $"Dat coc ho so {orderId} - Du an {RemoveDiacritics(project.ProjectName)}";

        var payment = new Payment
        {
            Id            = Guid.NewGuid(),
            UserId        = userId,
            ApplicationId = dto.ApplicationId,
            HousingProjectId = application.ProjectId,
            OrderId       = orderId,
            OrderInfo     = orderInfo,
            Amount        = depositAmount,
            Status        = "Pending",
            CreatedAt     = DateTime.UtcNow
        };

        await _paymentRepository.CreateAsync(payment);

        // ── 4. Tạo VNPay URL ────────────────────────────────────────────────
        var vnpRequest = new VnPaymentRequest
        {
            OrderId     = orderId,
            OrderInfo   = orderInfo,
            OrderType   = "deposit",
            Amount      = depositAmount,
            CreatedDate = DateTime.Now
        };

        var paymentUrl = _vnPayService.CreatePaymentUrl(httpContext, vnpRequest);

        _logger.LogInformation(
            "Payment created: OrderId={OrderId}, Amount={Amount}, AppId={AppId}.",
            orderId, depositAmount, dto.ApplicationId);

        return new PaymentResponseDto
        {
            Success    = true,
            Message    = "Tạo URL thanh toán thành công",
            PaymentUrl = paymentUrl,
            OrderId    = orderId,
            Amount     = depositAmount
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2. Xử lý callback từ VNPay
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc/>
    public async Task<bool> HandleCallbackAsync(IQueryCollection queryParams)
    {
        // ── 1. Xác minh chữ ký HMAC-SHA512 ───────────────────────────────
        var isValidSignature = _vnPayService.ValidateSignature(queryParams);
        if (!isValidSignature)
        {
            _logger.LogWarning("VNPay callback: invalid signature.");
            return false;
        }

        // ── 2. Lấy mã đơn hàng từ callback ───────────────────────────────
        var orderId = queryParams["vnp_TxnRef"].ToString();
        if (string.IsNullOrEmpty(orderId))
            return false;

        // ── 3. Tìm bản ghi Payment trong DB ──────────────────────────────
        var payment = await _paymentRepository.GetByOrderIdAsync(orderId);
        if (payment == null)
            return false;

        // ── 4. Đọc thông tin phản hồi VNPay ──────────────────────────────
        var responseCode       = queryParams["vnp_ResponseCode"].ToString();
        var transactionStatus  = queryParams["vnp_TransactionStatus"].ToString();
        var transactionNo      = queryParams["vnp_TransactionNo"].ToString();
        var bankCode           = queryParams["vnp_BankCode"].ToString();
        var bankTranNo         = queryParams["vnp_BankTranNo"].ToString();
        var cardType           = queryParams["vnp_CardType"].ToString();
        var payDate            = queryParams["vnp_PayDate"].ToString();

        // ── 5. Cập nhật Payment theo kết quả VNPay ────────────────────────
        payment.VnpResponseCode      = responseCode;
        payment.VnpTransactionStatus = transactionStatus;
        payment.VnpTransactionNo     = transactionNo;
        payment.VnpBankCode          = bankCode;
        payment.VnpBankTranNo        = bankTranNo;
        payment.VnpCardType          = cardType;
        payment.VnpPayDate           = payDate;

        if (responseCode == "00" && transactionStatus == "00")
        {
            payment.PaidAt = DateTime.UtcNow;

            if (payment.ApplicationId.HasValue)
            {
                // Đưa payment.Status = "Success" vào cùng transaction với
                // ProcessSuccessfulDepositAsync để nếu có lỗi (Cloudinary, DB...)
                // thì payment cũng không bị ghi nhầm là Success khi slotCode/pdfUrl chưa có
                await ProcessSuccessfulDepositAsync(payment);
            }
            else
            {
                payment.Status = "Success";
                await _paymentRepository.UpdateAsync(payment);
            }
        }
        else
        {
            payment.Status = responseCode switch
            {
                "24" => "Cancelled",
                _    => "Failed"
            };
            await _paymentRepository.UpdateAsync(payment);
        }

        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3. Tra cứu giao dịch
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc/>
    public async Task<PaymentInfoDto?> GetPaymentByOrderIdAsync(string orderId)
    {
        var payment = await _paymentRepository.GetByOrderIdAsync(orderId);
        if (payment == null) return null;

        return await MapToInfoDtoAsync(payment);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PaymentInfoDto>> GetPaymentsByUserIdAsync(Guid userId)
    {
        var payments = await _paymentRepository.GetByUserIdAsync(userId);
        var result = new List<PaymentInfoDto>();
        foreach (var p in payments)
        {
            result.Add(await MapToInfoDtoAsync(p));
        }
        return result;
    }

    /// <inheritdoc/>
    public async Task<DepositPaymentResultDto?> GetDepositResultAsync(string orderId)
    {
        var payment = await _paymentRepository.GetByOrderIdAsync(orderId);
        if (payment == null || payment.Status != "Success" || !payment.ApplicationId.HasValue)
            return null;

        var application = await _applicationRepo.GetByIdWithDetailsAsync(payment.ApplicationId.Value);
        if (application == null) return null;

        var project = await _projectRepo.GetByIdAsync(application.ProjectId);

        var agreement = await _agreementRepo.GetByApplicationIdAsync(application.ApplicationId);

        return new DepositPaymentResultDto
        {
            OrderId          = payment.OrderId,
            ApplicationId    = application.ApplicationId,
            Amount           = payment.Amount,
            SlotCode         = application.SlotCode ?? "",
            PdfUrl           = agreement?.PdfUrl ?? "",
            VnpTransactionNo = payment.VnpTransactionNo,
            PaidAt           = payment.PaidAt,
            ProjectName      = project?.ProjectName ?? "",
            ApplicantName    = application.FullName
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Private: Post-payment processing
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Xử lý sau thanh toán thành công:
    /// 1. Sinh SlotCode
    /// 2. Sinh PDF hợp đồng nguyên tắc
    /// 3. Tạo bản ghi PrincipleAgreement
    /// 4. Cập nhật HousingApplication → DEPOSIT_PAID
    /// 5. Ghi ApplicationStatusHistory
    /// Tất cả trong 1 DB transaction.
    /// </summary>
    private async Task ProcessSuccessfulDepositAsync(Payment payment)
    {
        _logger.LogInformation(
            "Processing successful deposit for Application {AppId}, OrderId={OrderId}.",
            payment.ApplicationId, payment.OrderId);

        var application = await _applicationRepo.GetByIdWithDetailsAsync(payment.ApplicationId!.Value);
        if (application == null)
        {
            _logger.LogError("Application {AppId} not found during post-payment.", payment.ApplicationId);
            return;
        }

        // Nếu đã xử lý rồi (idempotency guard)
        if (application.ApplicationStatus == ApplicationStatusConstants.DepositPaid)
        {
            _logger.LogInformation("Application {AppId} already DEPOSIT_PAID. Skipping.", application.ApplicationId);
            return;
        }

        var project = await _projectRepo.GetByIdAsync(application.ProjectId);
        if (project == null)
        {
            _logger.LogError("Project {ProjectId} not found during post-payment.", application.ProjectId);
            return;
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // ── 1. Sinh SlotCode ────────────────────────────────────────────
            var slotCode = await GenerateSlotCodeAsync(project);
            application.SlotCode = slotCode;

            // ── 2. Cập nhật payment Status = "Success" TRONG transaction ──
            payment.Status = "Success";
            payment.PaidAt ??= DateTime.UtcNow;
            await _paymentRepository.UpdateAsync(payment);

            // ── 3. Tìm Ward Manager name (nếu có OfficerId) ────────────────
            // Dùng navigation property đã include thay vì FindAsync để tránh
            // conflict EF Core tracking (entity đã được load từ GetByIdWithDetailsAsync)
            var wardManagerName = application.Officer?.FullName ?? "Ban Quản lý Dự án";

            // ── 3. Sinh PDF hợp đồng ───────────────────────────────────────
            var pdfUrl = await _pdfContractService.GenerateAndUploadContractAsync(
                application, project, slotCode,
                payment.Amount, payment.VnpTransactionNo,
                wardManagerName);

            // ── 4. Tạo PrincipleAgreement ───────────────────────────────────
            var agreement = new PrincipleAgreement
            {
                Id            = Guid.NewGuid(),
                ApplicationId = application.ApplicationId,
                PdfUrl        = pdfUrl,
                CreatedAt     = DateTime.UtcNow
            };
            await _context.PrincipleAgreements.AddAsync(agreement);

            // ── 5. Cập nhật trạng thái → DEPOSIT_PAID ──────────────────────
            var oldStatus = application.ApplicationStatus;
            application.ApplicationStatus = ApplicationStatusConstants.DepositPaid;
            application.UpdatedAt = DateTime.UtcNow;
            await _applicationRepo.UpdateAsync(application);

            // ── 6. Ghi lịch sử xét duyệt ──────────────────────────────────
            var history = new ApplicationStatusHistory
            {
                HistoryId     = Guid.NewGuid(),
                ApplicationId = application.ApplicationId,
                ChangedBy     = application.ApplicantId,
                Action        = ReviewActionConstants.DepositPayment,
                OldStatus     = oldStatus,
                NewStatus     = ApplicationStatusConstants.DepositPaid,
                Note          = $"Thanh toán đặt cọc thành công. OrderId: {payment.OrderId}, SlotCode: {slotCode}",
                ChangedAt     = DateTime.UtcNow
            };
            await _context.ApplicationStatusHistories.AddAsync(history);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "Post-payment completed: AppId={AppId}, SlotCode={SlotCode}, Status={Old}→{New}.",
                application.ApplicationId, slotCode, oldStatus, ApplicationStatusConstants.DepositPaid);

            // ── 7. Gửi thông báo cho Applicant (sau commit) ─────────────
            await _notificationService.SendAsync(
                application.ApplicantId,
                "Thanh toán đặt cọc thành công",
                $"Mã suất bốc thăm: {slotCode}. Hợp đồng nguyên tắc đã được tạo.",
                NotificationTypeConstants.DepositPaid);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex,
                "Post-payment processing failed for Application {AppId}.", payment.ApplicationId);
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Private helpers
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sinh SlotCode: NOXH-{WardPrefix}-{Seq:000}
    /// Ví dụ: NOXH-BT-001, NOXH-BT-002, ...
    /// WardPrefix = 2 ký tự đầu của Ward viết hoa, không dấu.
    /// Seq = đếm số SlotCode đã có trong project + 1.
    /// </summary>
    private async Task<string> GenerateSlotCodeAsync(HousingProject project)
    {
        // Lấy 2 ký tự đầu của Ward (viết hoa, không dấu)
        var wardPrefix = RemoveDiacritics(project.Ward ?? "XX")
            .Replace(" ", "")
            .ToUpperInvariant();
        wardPrefix = wardPrefix.Length >= 2 ? wardPrefix[..2] : wardPrefix.PadRight(2, 'X');

        // Đếm số application đã có SlotCode trong project này
        var existingCount = await _context.HousingApplications
            .CountAsync(a => a.ProjectId == project.Id && a.SlotCode != null);

        var seq = existingCount + 1;
        return $"NOXH-{wardPrefix}-{seq:D3}";
    }

    /// <summary>
    /// Tạo mã đơn hàng duy nhất: yyyyMMddHHmmss + 4 chữ số random.
    /// </summary>
    private static string GenerateOrderId()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var random    = new Random().Next(1000, 9999);
        return $"{timestamp}{random}";
    }

    /// <summary>Map Payment entity sang PaymentInfoDto, kèm SlotCode/PdfUrl nếu có.</summary>
    private async Task<PaymentInfoDto> MapToInfoDtoAsync(Payment payment)
    {
        var dto = new PaymentInfoDto
        {
            Id               = payment.Id,
            OrderId          = payment.OrderId,
            OrderInfo        = payment.OrderInfo,
            Amount           = payment.Amount,
            Status           = payment.Status,
            ApplicationId    = payment.ApplicationId,
            VnpResponseCode  = payment.VnpResponseCode,
            VnpTransactionNo = payment.VnpTransactionNo,
            VnpBankCode      = payment.VnpBankCode,
            VnpPayDate       = payment.VnpPayDate,
            CreatedAt        = payment.CreatedAt,
            PaidAt           = payment.PaidAt
        };

        // Enrich với SlotCode & PdfUrl nếu có ApplicationId
        if (payment.ApplicationId.HasValue && payment.Status == "Success")
        {
            var application = await _applicationRepo.GetByIdWithDetailsAsync(payment.ApplicationId.Value);
            if (application != null)
            {
                dto.SlotCode = application.SlotCode;

                var agreement = await _agreementRepo.GetByApplicationIdAsync(application.ApplicationId);
                dto.PdfUrl = agreement?.PdfUrl;
            }
        }

        return dto;
    }

    /// <summary>Bỏ dấu tiếng Việt (dùng cho OrderInfo và SlotCode prefix).</summary>
    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();

        foreach (var c in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString()
            .Normalize(System.Text.NormalizationForm.FormC)
            .Replace("đ", "d")
            .Replace("Đ", "D");
    }
}
