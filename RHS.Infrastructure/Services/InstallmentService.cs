using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.Installment;
using RHS.Application.DTOs.Payment;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Triển khai IInstallmentService — quản lý lịch đóng tiền theo đợt.
/// Pattern: Event-Driven + Template
///   - PaymentMilestone (template) → PaymentInstallment (actual) → Payment (VNPay)
/// </summary>
public class InstallmentService : IInstallmentService
{
    private readonly AppDbContext _db;
    private readonly IVnPayService _vnPayService;
    private readonly IPaymentRepository _paymentRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<InstallmentService> _logger;

    public InstallmentService(
        AppDbContext db,
        IVnPayService vnPayService,
        IPaymentRepository paymentRepository,
        INotificationService notificationService,
        ILogger<InstallmentService> logger)
    {
        _db                  = db;
        _vnPayService        = vnPayService;
        _paymentRepository   = paymentRepository;
        _notificationService = notificationService;
        _logger              = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1. FireTriggerEvent — sinh PaymentInstallment từ milestone templates
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    /// <inheritdoc />
    public async Task FireTriggerEventAsync(
        Guid applicationId, string triggerEvent, DateTime eventDate)
    {
        _logger.LogInformation(
            "FireTriggerEvent: App={AppId}, Event={Event}, Date={Date}.",
            applicationId, triggerEvent, eventDate);

        var app = await _db.HousingApplications
            .Include(a => a.Apartment)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId)
            ?? throw new InvalidOperationException($"Hồ sơ {applicationId} không tồn tại.");

        await EnsureDefaultMilestonesAsync(app.ProjectId);

        // Tự động sinh hoặc đồng bộ 6 đợt cho hồ sơ
        await EnsureInstallmentsForApplicationAsync(applicationId);

        // Trường hợp 1: Khi trúng bốc thăm / cấp nhà (ON_LOTTERY_WON)
        if (string.Equals(triggerEvent, TriggerEventConstants.OnLotteryWon, StringComparison.OrdinalIgnoreCase))
        {
            var d1Inst = await _db.PaymentInstallments
                .Include(i => i.Milestone)
                .FirstOrDefaultAsync(i => i.ApplicationId == applicationId && i.Milestone.PhaseOrder == 1);

            // Cập nhật trạng thái hồ sơ sang DEPOSIT_PENDING nếu đang APPROVED
            if (app.ApplicationStatus == ApplicationStatusConstants.Approved
                || app.ApplicationStatus == ApplicationStatusConstants.ApprovedByTimeout)
            {
                app.ApplicationStatus = ApplicationStatusConstants.DepositPending;
                app.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            if (d1Inst != null && d1Inst.Status == InstallmentStatusConstants.Pending)
            {
                try
                {
                    await _notificationService.SendAsync(
                        app.ApplicantId,
                        "🎉 Trúng bốc thăm / Cấp nhà - Thông báo đóng cọc (Đợt 1)",
                        $"Chúc mừng bạn! Khoản cọc Đợt 1: {d1Inst.Amount:N0} VND. Hạn đóng: {d1Inst.DueDate:dd/MM/yyyy}.",
                        NotificationTypeConstants.InstallmentCreated);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send notification for D1 installment {Id}.", d1Inst.Id);
                }
            }
            return;
        }

        // Trường hợp 2: Khi Ký Hợp đồng (ON_CONTRACT_SIGNED) → Unlock Đợt 2 (LOCKED → PENDING)
        if (string.Equals(triggerEvent, TriggerEventConstants.OnContractSigned, StringComparison.OrdinalIgnoreCase))
        {
            var d2Inst = await _db.PaymentInstallments
                .Include(i => i.Milestone)
                .FirstOrDefaultAsync(i => i.ApplicationId == applicationId && i.Milestone.PhaseOrder == 2);

            if (d2Inst != null)
            {
                if (d2Inst.Status == InstallmentStatusConstants.Locked)
                {
                    d2Inst.Status = InstallmentStatusConstants.Pending;
                    d2Inst.StartDate = eventDate;
                    d2Inst.DueDate = eventDate.AddDays(d2Inst.Milestone.DueDays);
                    d2Inst.UpdatedAt = DateTime.UtcNow;

                    await _db.SaveChangesAsync();
                }

                try
                {
                    await _notificationService.SendAsync(
                        app.ApplicantId,
                        "📝 Ký hợp đồng thành công - Mở thanh toán Đợt 2",
                        $"Ký Hợp đồng thành công. Khoản thanh toán Đợt 2: {d2Inst.Amount:N0} VND. Hạn đóng: {d2Inst.DueDate:dd/MM/yyyy}.",
                        NotificationTypeConstants.InstallmentCreated);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed notification for D2 installment {Id}", d2Inst.Id);
                }
            }
            return;
        }

        // Trường hợp 3: Sự kiện tiến độ khác → Gọi UnlockPhaseByEventAsync
        await UnlockPhaseByEventAsync(app.ProjectId, triggerEvent);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2. GetSummary — tổng hợp lịch đóng tiền theo hồ sơ
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<InstallmentSummaryDto?> GetSummaryAsync(Guid applicationId)
    {
        // Tự động sinh lịch 6 đợt & đồng bộ mở Đợt 2 nếu đã ký hợp đồng
        await EnsureInstallmentsForApplicationAsync(applicationId);

        // Self-heal: payment đã Paid nhưng installment còn PENDING (lỗi history ChangedBy)
        await HealPaidInstallmentsForApplicationAsync(applicationId);

        var app = await _db.HousingApplications
            .AsNoTracking()
            .Include(a => a.Apartment)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

        if (app == null) return null;

        var apartment = app.Apartment;
        if (apartment == null && app.ApartmentId.HasValue)
        {
            apartment = await _db.Apartments
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == app.ApartmentId.Value);
        }

        var installments = await _db.PaymentInstallments
            .AsNoTracking()
            .Include(i => i.Milestone)
            .Where(i => i.ApplicationId == applicationId)
            .OrderBy(i => i.Milestone != null ? i.Milestone.PhaseOrder : 0)
            .ToListAsync();

        var now = DateTime.UtcNow;

        const decimal dailyRate = 0.0005m; // 0.05% / ngày

        var phases = installments.Select(i =>
        {
            int overdueDays = 0;
            decimal penaltyAmount = 0m;

            if ((i.Status == InstallmentStatusConstants.Pending || i.Status == InstallmentStatusConstants.Overdue) && now > i.DueDate)
            {
                overdueDays = (int)Math.Floor((now - i.DueDate).TotalDays);
                if (overdueDays > 0)
                {
                    penaltyAmount = Math.Round(i.Amount * dailyRate * overdueDays, 0, MidpointRounding.AwayFromZero);
                }
            }

            return new InstallmentDto
            {
                Id                 = i.Id,
                PhaseOrder         = i.Milestone?.PhaseOrder ?? 0,
                PhaseName          = i.Milestone?.PhaseName ?? $"Đợt",
                Amount             = i.Amount,
                StartDate          = i.StartDate,
                DueDate            = i.DueDate,
                Status             = i.Status,
                PaidAt             = i.PaidAt,
                RemainingDays      = (int)(i.DueDate - now).TotalDays,
                OverdueDays        = overdueDays,
                DailyPenaltyRate   = dailyRate,
                PenaltyAmount      = penaltyAmount,
                TotalPayableAmount = i.Amount + penaltyAmount,
                Note               = i.Note
            };
        }).ToList();

        var totalUnpaidPenalties = phases
            .Where(p => p.Status != InstallmentStatusConstants.Paid && p.Status != InstallmentStatusConstants.Cancelled)
            .Sum(p => p.PenaltyAmount);

        var totalRemainingPrincipal = phases
            .Where(p => p.Status != InstallmentStatusConstants.Paid && p.Status != InstallmentStatusConstants.Cancelled)
            .Sum(p => p.Amount);

        return new InstallmentSummaryDto
        {
            ApplicationId          = applicationId,
            ApartmentTypeName      = apartment?.UnitName,
            ApartmentArea          = apartment?.Area,
            ApartmentPrice         = apartment?.Price,
            TotalAmount            = phases.Sum(p => p.Amount),
            TotalPaid              = phases.Where(p => p.Status == InstallmentStatusConstants.Paid).Sum(p => p.Amount),
            TotalRemaining         = totalRemainingPrincipal,
            TotalPenalty           = totalUnpaidPenalties,
            TotalAmountWithPenalty = totalRemainingPrincipal + totalUnpaidPenalties,
            TotalPhases            = phases.Count,
            PaidPhases             = phases.Count(p => p.Status == InstallmentStatusConstants.Paid),
            Phases                 = phases
        };
    }

    /// <summary>
    /// Nếu có Payment Paid gắn InstId mà installment vẫn PENDING/OVERDUE → chạy lại ProcessInstallmentPaid.
    /// </summary>
    private async Task HealPaidInstallmentsForApplicationAsync(Guid applicationId)
    {
        var unpaidIds = await _db.PaymentInstallments
            .Where(i => i.ApplicationId == applicationId
                        && (i.Status == InstallmentStatusConstants.Pending
                            || i.Status == InstallmentStatusConstants.Overdue))
            .Select(i => i.Id)
            .ToListAsync();

        if (unpaidIds.Count == 0) return;

        var paidPayments = await _db.Payments
            .Where(p => p.ApplicationId == applicationId
                        && (p.Status == "Paid" || p.Status == "Success"))
            .ToListAsync();

        foreach (var payment in paidPayments)
        {
            if (!TryParseInstallmentId(payment.OrderInfo, out var instId))
                continue;
            if (!unpaidIds.Contains(instId))
                continue;

            try
            {
                await ProcessInstallmentPaidAsync(instId, payment.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "HealPaidInstallments failed: AppId={AppId}, InstId={InstId}, OrderId={OrderId}.",
                    applicationId, instId, payment.OrderId);
            }
        }
    }

    private static bool TryParseInstallmentId(string? orderInfo, out Guid installmentId)
    {
        installmentId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(orderInfo))
            return false;

        const string marker = "InstId:";
        var idx = orderInfo.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return false;

        var raw = orderInfo[(idx + marker.Length)..].Trim();
        var token = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? raw;
        return Guid.TryParse(token, out installmentId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3. CreateInstallmentPayment — tạo URL VNPay cho đợt cụ thể
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<PaymentResponseDto> CreateInstallmentPaymentAsync(
        Guid userId, Guid installmentId, HttpContext httpContext)
    {
        var installment = await _db.PaymentInstallments
            .Include(i => i.HousingApplication)
                .ThenInclude(a => a.HousingProject)
            .Include(i => i.Milestone)
            .FirstOrDefaultAsync(i => i.Id == installmentId);

        if (installment == null)
            return Fail($"Không tìm thấy khoản thu với ID: {installmentId}");

        // Chỉ chủ hồ sơ mới được thanh toán
        if (installment.HousingApplication.ApplicantId != userId)
            return Fail("Bạn không phải chủ hồ sơ này.");

        // Chỉ cho thanh toán PENDING hoặc OVERDUE
        if (installment.Status == InstallmentStatusConstants.Locked)
            return Fail("Đợt thanh toán này đang bị khóa. Vui lòng chờ Chủ đầu tư kích hoạt tiến độ và hoàn tất các đợt trước đó.");

        if (installment.Status != InstallmentStatusConstants.Pending
            && installment.Status != InstallmentStatusConstants.Overdue)
            return Fail($"Khoản thu đang ở trạng thái {installment.Status}, không thể thanh toán.");

        // Kiểm tra đợt trước đã thanh toán chưa (tuần tự)
        var previousUnpaid = await _db.PaymentInstallments
            .Include(i => i.Milestone)
            .AnyAsync(i => i.ApplicationId == installment.ApplicationId
                        && i.Milestone.PhaseOrder < installment.Milestone.PhaseOrder
                        && i.Status != InstallmentStatusConstants.Paid
                        && i.Status != InstallmentStatusConstants.Cancelled);

        if (previousUnpaid)
            return Fail("Vui lòng thanh toán các đợt trước đó trước.");

        // Phiên Pending cũ của cùng đợt → hủy, tạo phiên VNPay mới (thoát app không bị khóa)
        var stalePendings = await _db.Payments
            .Where(p => p.ApplicationId == installment.ApplicationId
                        && p.Status == "Pending"
                        && p.OrderInfo.Contains(installmentId.ToString()))
            .ToListAsync();

        foreach (var pending in stalePendings)
        {
            pending.Status = "Cancelled";
            pending.VnpResponseCode ??= "99";
            await _paymentRepository.UpdateAsync(pending);
        }

        // Tính tiền lãi phạt trễ hạn (0.05%/ngày) nếu đợt đã quá hạn
        var now = DateTime.UtcNow;
        decimal penaltyAmount = 0m;
        if ((installment.Status == InstallmentStatusConstants.Pending || installment.Status == InstallmentStatusConstants.Overdue) && now > installment.DueDate)
        {
            var overdueDays = (int)Math.Floor((now - installment.DueDate).TotalDays);
            if (overdueDays > 0)
            {
                penaltyAmount = Math.Round(installment.Amount * 0.0005m * overdueDays, 0, MidpointRounding.AwayFromZero);
            }
        }

        var totalPayableAmount = installment.Amount + penaltyAmount;

        // Tạo Payment record
        var orderId = GenerateOrderId();
        var projectName = RemoveDiacritics(
            installment.HousingApplication.HousingProject.ProjectName);
        var orderInfo = penaltyAmount > 0
            ? $"TT {installment.Milestone.PhaseName} (Goc:{installment.Amount:N0} + LaiPhat:{penaltyAmount:N0}) - {orderId} - {projectName} - InstId:{installmentId}"
            : $"TT {installment.Milestone.PhaseName} - {orderId} - {projectName} - InstId:{installmentId}";

        var payment = new Payment
        {
            Id               = Guid.NewGuid(),
            UserId           = userId,
            ApplicationId    = installment.ApplicationId,
            HousingProjectId = installment.HousingApplication.ProjectId,
            OrderId          = orderId,
            OrderInfo        = orderInfo,
            Amount           = totalPayableAmount,
            Status           = "Pending",
            CreatedAt        = DateTime.UtcNow
        };

        await _paymentRepository.CreateAsync(payment);

        // Tạo VNPay URL
        var vnpRequest = new VnPaymentRequest
        {
            OrderId     = orderId,
            OrderInfo   = orderInfo,
            OrderType   = "installment",
            Amount      = totalPayableAmount,
            CreatedDate = DateTime.UtcNow
        };

        var paymentUrl = _vnPayService.CreatePaymentUrl(httpContext, vnpRequest);

        _logger.LogInformation(
            "Installment payment created: OrderId={OrderId}, InstallmentId={InstId}, Amount={Amount}.",
            orderId, installmentId, installment.Amount);

        return new PaymentResponseDto
        {
            Success    = true,
            Message    = "Tạo phiên thanh toán mới (hạn VNPay 30 phút).",
            PaymentUrl = paymentUrl,
            OrderId    = orderId,
            Amount     = installment.Amount
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4. ProcessInstallmentPaid — callback VNPay thành công
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task ProcessInstallmentPaidAsync(Guid installmentId, Guid paymentId)
    {
        var installment = await _db.PaymentInstallments
            .Include(i => i.HousingApplication)
            .Include(i => i.Milestone)
            .FirstOrDefaultAsync(i => i.Id == installmentId)
            ?? throw new InvalidOperationException($"Installment {installmentId} không tồn tại.");

        var applicantId = installment.HousingApplication.ApplicantId;
        var alreadyPaid = installment.Status == InstallmentStatusConstants.Paid;

        if (!alreadyPaid)
        {
            installment.Status    = InstallmentStatusConstants.Paid;
            installment.PaidAt    = DateTime.UtcNow;
            installment.PaymentId = paymentId;
            installment.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Installment PAID: Id={Id}, Phase={Phase}, Amount={Amount}, App={AppId}.",
                installmentId, installment.Milestone.PhaseName, installment.Amount,
                installment.ApplicationId);

            // ChangedBy bắt buộc (FK Users) — thiếu sẽ làm SaveChanges fail, đợt vẫn PENDING
            _db.Set<ApplicationStatusHistory>().Add(new ApplicationStatusHistory
            {
                HistoryId     = Guid.NewGuid(),
                ApplicationId = installment.ApplicationId,
                ChangedBy     = applicantId,
                OldStatus     = installment.HousingApplication.ApplicationStatus,
                NewStatus     = installment.HousingApplication.ApplicationStatus,
                Action        = ReviewActionConstants.InstallmentPayment,
                Note          = $"Thanh toán {installment.Milestone.PhaseName}: {installment.Amount:N0} VND",
                ChangedAt     = DateTime.UtcNow
            });

            try
            {
                await _notificationService.SendAsync(
                    applicantId,
                    $"✅ Thanh toán thành công: {installment.Milestone.PhaseName}",
                    $"Đã thanh toán {installment.Amount:N0} VND cho {installment.Milestone.PhaseName}.",
                    NotificationTypeConstants.InstallmentPaid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send paid notification for installment {Id}.", installmentId);
            }
        }
        else
        {
            // Self-heal: payment đã Paid nhưng lần trước SaveChanges fail giữa chừng
            installment.PaymentId ??= paymentId;
            installment.PaidAt ??= DateTime.UtcNow;
            installment.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation(
                "Installment {Id} already PAID — ensuring FULLY_PAID check / self-heal.",
                installmentId);
        }

        // Kiểm tra xem tất cả đợt đã PAID chưa → FULLY_PAID
        var allInstallments = await _db.PaymentInstallments
            .Where(i => i.ApplicationId == installment.ApplicationId)
            .ToListAsync();

        var allPaid = allInstallments.All(i =>
            i.Status == InstallmentStatusConstants.Paid
            || i.Status == InstallmentStatusConstants.Cancelled);

        var application = installment.HousingApplication;
        if (allPaid
            && allInstallments.Count > 0
            && application.ApplicationStatus != ApplicationStatusConstants.FullyPaid)
        {
            var oldStatus = application.ApplicationStatus;
            application.ApplicationStatus = ApplicationStatusConstants.FullyPaid;
            application.UpdatedAt = DateTime.UtcNow;

            _db.Set<ApplicationStatusHistory>().Add(new ApplicationStatusHistory
            {
                HistoryId     = Guid.NewGuid(),
                ApplicationId = application.ApplicationId,
                ChangedBy     = applicantId,
                OldStatus     = oldStatus,
                NewStatus     = ApplicationStatusConstants.FullyPaid,
                Action        = ReviewActionConstants.InstallmentPayment,
                Note          = "Đã thanh toán đủ toàn bộ đợt trả trước.",
                ChangedAt     = DateTime.UtcNow
            });

            _logger.LogInformation(
                "Application {AppId} is now FULLY_PAID.", application.ApplicationId);

            try
            {
                await _notificationService.SendAsync(
                    applicantId,
                    "🎉 Thanh toán đủ toàn bộ đợt trả trước!",
                    "Bạn đã hoàn thành thanh toán tất cả đợt. Chúc mừng bạn!",
                    NotificationTypeConstants.FullyPaid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send fully-paid notification for app {AppId}.",
                    application.ApplicationId);
            }
        }

        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5. ProcessOverdueInstallments — Background worker gọi mỗi đêm
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task ProcessOverdueInstallmentsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var overdueInstallments = await _db.PaymentInstallments
            .Include(i => i.HousingApplication)
            .Include(i => i.Milestone)
            .Where(i => i.Status == InstallmentStatusConstants.Pending
                      && i.DueDate < now)
            .ToListAsync(ct);

        if (overdueInstallments.Count == 0)
        {
            _logger.LogDebug("No overdue installments found.");
            return;
        }

        _logger.LogInformation(
            "Found {Count} overdue installments to process.", overdueInstallments.Count);

        foreach (var inst in overdueInstallments)
        {
            inst.Status = InstallmentStatusConstants.Overdue;
            inst.UpdatedAt = now;

            _logger.LogWarning(
                "Installment OVERDUE: Id={Id}, Phase={Phase}, Amount={Amount}, App={AppId}, DueDate={Due}.",
                inst.Id, inst.Milestone.PhaseName, inst.Amount,
                inst.ApplicationId, inst.DueDate);

            try
            {
                await _notificationService.SendAsync(
                    inst.HousingApplication.ApplicantId,
                    $"⚠️ Khoản thu quá hạn: {inst.Milestone.PhaseName}",
                    $"Khoản thu {inst.Amount:N0} VND đã quá hạn từ {inst.DueDate:dd/MM/yyyy}. "
                    + "Vui lòng thanh toán sớm nhất có thể.",
                    NotificationTypeConstants.InstallmentOverdue);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to send overdue notification for installment {Id}.", inst.Id);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 6. Xử lý Hủy hợp đồng, Phạt cọc & Hoàn tiền (Luồng xấu)
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<ContractCancellationPreviewDto> PreviewContractCancellationAsync(Guid applicationId)
    {
        var app = await _db.HousingApplications
            .AsNoTracking()
            .Include(a => a.Applicant)
            .Include(a => a.Apartment)
            .Include(a => a.HousingProject)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

        if (app == null)
        {
            return new ContractCancellationPreviewDto
            {
                ApplicationId = applicationId,
                CanCancel = false,
                Message = "Hồ sơ không tồn tại."
            };
        }

        var summary = await GetSummaryAsync(applicationId);
        var phases = summary?.Phases ?? new List<InstallmentDto>();

        int overdueCount = phases.Count(p => p.OverdueDays > 0 || p.Status == InstallmentStatusConstants.Overdue);
        bool isEligibleForForced = overdueCount >= 2;

        var d1 = phases.FirstOrDefault(p => p.PhaseOrder == 1);
        decimal phase1Amount = d1?.Amount ?? 0m;
        decimal phase1PaidAmount = d1?.Status == InstallmentStatusConstants.Paid ? d1.Amount : 0m;
        decimal depositForfeited = phase1PaidAmount; // Phạt cọc = Toàn bộ tiền cọc Đợt 1 đã đóng

        decimal phase2PlusPaid = phases
            .Where(p => p.PhaseOrder > 1 && p.Status == InstallmentStatusConstants.Paid)
            .Sum(p => p.Amount);

        decimal totalUnpaidPenalty = phases
            .Where(p => p.Status != InstallmentStatusConstants.Paid && p.Status != InstallmentStatusConstants.Cancelled)
            .Sum(p => p.PenaltyAmount);

        decimal refundAmount = Math.Max(0m, phase2PlusPaid - totalUnpaidPenalty);

        bool canCancel = app.ApplicationStatus != ApplicationStatusConstants.Canceled
                      && app.ApplicationStatus != ApplicationStatusConstants.Rejected;

        // Tìm ứng viên tiếp theo trong Waitlist (nếu có)
        var nextWaitlistCandidate = await _db.HousingApplications
            .AsNoTracking()
            .Include(a => a.Applicant)
            .Where(a => a.ProjectId == app.ProjectId
                     && a.WaitlistNumber.HasValue
                     && a.WaitlistNumber > 0
                     && a.ApplicationStatus != ApplicationStatusConstants.Canceled
                     && a.ApplicationStatus != ApplicationStatusConstants.Rejected)
            .OrderBy(a => a.WaitlistNumber)
            .FirstOrDefaultAsync();

        var message = !canCancel
            ? $"Hồ sơ đang ở trạng thái {app.ApplicationStatus}, không thể hủy hợp đồng."
            : isEligibleForForced
                ? $"Đủ điều kiện cưỡng chế thu hồi căn ({overdueCount} đợt quá hạn). Phạt cọc Đợt 1: {depositForfeited:N0} VND. Tiền thực hoàn: {refundAmount:N0} VND."
                : $"Tự nguyện hủy HĐ. Phạt cọc Đợt 1: {depositForfeited:N0} VND. Tiền thực hoàn: {refundAmount:N0} VND.";

        return new ContractCancellationPreviewDto
        {
            ApplicationId = applicationId,
            ApplicantName = app.FullName,
            ApartmentUnitName = app.Apartment?.UnitName,
            ApartmentPrice = app.Apartment?.Price,
            CurrentApplicationStatus = app.ApplicationStatus,
            CanCancel = canCancel,
            Message = message,
            OverduePhasesCount = overdueCount,
            IsEligibleForForcedRevocation = isEligibleForForced,
            Phase1Amount = phase1Amount,
            Phase1PaidAmount = phase1PaidAmount,
            DepositForfeited = depositForfeited,
            Phase2PlusPaidAmount = phase2PlusPaid,
            TotalUnpaidPenalty = totalUnpaidPenalty,
            RefundAmount = refundAmount,
            PromotedWaitlistApplicantName = nextWaitlistCandidate?.FullName ?? nextWaitlistCandidate?.Applicant?.FullName,
            Installments = phases
        };
    }

    /// <inheritdoc />
    public async Task<ContractCancellationResultDto> SubmitCancellationRequestAsync(
        Guid userId, Guid applicationId, CancelContractRequestDto dto)
    {
        var app = await _db.HousingApplications
            .Include(a => a.Apartment)
            .Include(a => a.HousingProject)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

        if (app == null)
        {
            return new ContractCancellationResultDto
            {
                Success = false,
                Message = "Hồ sơ không tồn tại.",
                ApplicationId = applicationId
            };
        }

        if (app.ApplicantId != userId)
        {
            return new ContractCancellationResultDto
            {
                Success = false,
                Message = "Bạn không phải chủ hồ sơ này.",
                ApplicationId = applicationId
            };
        }

        if (app.ApplicationStatus == ApplicationStatusConstants.CancellationRequested)
        {
            return new ContractCancellationResultDto
            {
                Success = false,
                Message = "Bạn đã nộp đơn xin ngừng thanh toán trước đó. Đơn đang chờ Chủ đầu tư phê duyệt.",
                ApplicationId = applicationId
            };
        }

        var invalidStatuses = new[]
        {
            ApplicationStatusConstants.Draft,
            ApplicationStatusConstants.Canceled,
            ApplicationStatusConstants.Rejected,
            ApplicationStatusConstants.Expired
        };

        if (invalidStatuses.Contains(app.ApplicationStatus))
        {
            return new ContractCancellationResultDto
            {
                Success = false,
                Message = $"Hồ sơ đang ở trạng thái {app.ApplicationStatus}, không thể nộp đơn xin ngừng thanh toán.",
                ApplicationId = applicationId
            };
        }

        var oldStatus = app.ApplicationStatus;
        app.ApplicationStatus = ApplicationStatusConstants.CancellationRequested;
        app.UpdatedAt = DateTime.UtcNow;

        var noteInfo = $"Nộp đơn xin ngừng thanh toán / rút hồ sơ. Lý do: {dto.Reason}. STK: {dto.BankAccountNumber} - Ngân hàng: {dto.BankName} - Chủ TK: {dto.AccountHolderName}";

        _db.Set<ApplicationStatusHistory>().Add(new ApplicationStatusHistory
        {
            HistoryId = Guid.NewGuid(),
            ApplicationId = applicationId,
            ChangedBy = userId,
            OldStatus = oldStatus,
            NewStatus = ApplicationStatusConstants.CancellationRequested,
            Action = "SUBMIT_CANCELLATION_REQUEST",
            Note = noteInfo,
            ChangedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        try
        {
            var developerId = app.HousingProject?.DeveloperId;
            if (developerId.HasValue && developerId.Value != Guid.Empty)
            {
                await _notificationService.SendAsync(
                    developerId.Value,
                    "📩 Đơn xin ngừng thanh toán mới",
                    $"Người dân {app.FullName} (Mã căn: {app.Apartment?.UnitName ?? "Chưa gán"}) đã gửi đơn xin ngừng thanh toán / rút hồ sơ. Vui lòng thẩm định & duyệt.",
                    NotificationTypeConstants.ApplicationNeedMoreDocs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notification to Developer for cancellation request on app {AppId}", applicationId);
        }

        var preview = await PreviewContractCancellationAsync(applicationId);

        return new ContractCancellationResultDto
        {
            Success = true,
            Message = "Đã gửi đơn xin ngừng thanh toán thành công. Đơn đang chờ Chủ đầu tư thẩm định & phê duyệt.",
            ApplicationId = applicationId,
            DepositForfeited = preview.DepositForfeited,
            RefundAmount = preview.RefundAmount,
            TotalPenaltyDeducted = preview.TotalUnpaidPenalty,
            CancelledAt = DateTime.UtcNow
        };
    }

    /// <inheritdoc />
    public async Task<List<CancellationRequestItemDto>> GetPendingCancellationRequestsAsync(Guid projectId)
    {
        var pendingApps = await _db.HousingApplications
            .AsNoTracking()
            .Include(a => a.Applicant)
            .Include(a => a.Apartment)
            .Where(a => a.ProjectId == projectId && a.ApplicationStatus == ApplicationStatusConstants.CancellationRequested)
            .ToListAsync();

        var result = new List<CancellationRequestItemDto>();

        foreach (var app in pendingApps)
        {
            var preview = await PreviewContractCancellationAsync(app.ApplicationId);

            var lastRequestHistory = await _db.ApplicationStatusHistories
                .AsNoTracking()
                .Where(h => h.ApplicationId == app.ApplicationId && h.NewStatus == ApplicationStatusConstants.CancellationRequested)
                .OrderByDescending(h => h.ChangedAt)
                .FirstOrDefaultAsync();

            result.Add(new CancellationRequestItemDto
            {
                ApplicationId = app.ApplicationId,
                ApplicantName = app.FullName,
                CitizenId = app.CitizenId,
                PhoneNumber = app.Applicant?.PhoneNumber,
                ApartmentUnitName = app.Apartment?.UnitName,
                Reason = lastRequestHistory?.Note ?? "Xin ngừng thanh toán / rút hồ sơ",
                Phase1DepositForfeited = preview.DepositForfeited,
                Phase2PlusPaidAmount = preview.Phase2PlusPaidAmount,
                UnpaidPenaltyAmount = preview.TotalUnpaidPenalty,
                NetRefundAmount = preview.RefundAmount,
                RequestedAt = lastRequestHistory?.ChangedAt ?? app.UpdatedAt ?? DateTime.UtcNow
            });
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<ContractCancellationResultDto> ApproveCancellationRequestAsync(
        Guid userId, Guid applicationId)
    {
        var app = await _db.HousingApplications
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

        if (app == null)
        {
            return new ContractCancellationResultDto
            {
                Success = false,
                Message = "Hồ sơ không tồn tại.",
                ApplicationId = applicationId
            };
        }

        if (app.ApplicationStatus != ApplicationStatusConstants.CancellationRequested)
        {
            return new ContractCancellationResultDto
            {
                Success = false,
                Message = $"Hồ sơ đang ở trạng thái {app.ApplicationStatus}, không thể phê duyệt đơn xin ngừng thanh toán.",
                ApplicationId = applicationId
            };
        }

        var requestDto = new CancelContractRequestDto
        {
            Reason = "Chủ đầu tư chấp thuận đơn xin ngừng thanh toán / rút hồ sơ",
            IsForcedRevocation = false
        };

        return await CancelContractAndProcessRefundAsync(userId, applicationId, requestDto);
    }

    /// <inheritdoc />
    public async Task<ContractCancellationResultDto> RejectCancellationRequestAsync(
        Guid userId, Guid applicationId, RejectCancellationRequestDto dto)
    {
        var app = await _db.HousingApplications
            .Include(a => a.Applicant)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

        if (app == null)
        {
            return new ContractCancellationResultDto
            {
                Success = false,
                Message = "Hồ sơ không tồn tại.",
                ApplicationId = applicationId
            };
        }

        if (app.ApplicationStatus != ApplicationStatusConstants.CancellationRequested)
        {
            return new ContractCancellationResultDto
            {
                Success = false,
                Message = $"Hồ sơ đang ở trạng thái {app.ApplicationStatus}, không thể từ chối đơn xin ngừng thanh toán.",
                ApplicationId = applicationId
            };
        }

        var lastReqHistory = await _db.ApplicationStatusHistories
            .AsNoTracking()
            .Where(h => h.ApplicationId == applicationId && h.NewStatus == ApplicationStatusConstants.CancellationRequested)
            .OrderByDescending(h => h.ChangedAt)
            .FirstOrDefaultAsync();

        var previousStatus = lastReqHistory?.OldStatus ?? ApplicationStatusConstants.DepositPaid;

        app.ApplicationStatus = previousStatus;
        app.UpdatedAt = DateTime.UtcNow;

        _db.Set<ApplicationStatusHistory>().Add(new ApplicationStatusHistory
        {
            HistoryId = Guid.NewGuid(),
            ApplicationId = applicationId,
            ChangedBy = userId,
            OldStatus = ApplicationStatusConstants.CancellationRequested,
            NewStatus = previousStatus,
            Action = "REJECT_CANCELLATION_REQUEST",
            Note = $"Từ chối đơn xin ngừng thanh toán. Lý do từ chối: {dto.Reason}",
            ChangedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        try
        {
            await _notificationService.SendAsync(
                app.ApplicantId,
                "❌ Đơn xin ngừng thanh toán bị từ chối",
                $"Đơn xin ngừng thanh toán / rút hồ sơ của bạn đã bị Chủ đầu tư từ chối. Lý do: {dto.Reason}.",
                NotificationTypeConstants.ApplicationNeedMoreDocs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send rejection notification for app {AppId}", applicationId);
        }

        return new ContractCancellationResultDto
        {
            Success = true,
            Message = $"Đã từ chối đơn xin ngừng thanh toán của người dân. Hồ sơ được khôi phục về trạng thái {previousStatus}.",
            ApplicationId = applicationId,
            CancelledAt = DateTime.UtcNow
        };
    }

    /// <inheritdoc />
    public async Task<ContractCancellationResultDto> CancelContractAndProcessRefundAsync(
        Guid userId, Guid applicationId, CancelContractRequestDto dto)
    {
        var app = await _db.HousingApplications
            .Include(a => a.Apartment)
            .Include(a => a.HousingProject)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

        if (app == null)
        {
            return new ContractCancellationResultDto
            {
                Success = false,
                Message = "Hồ sơ không tồn tại.",
                ApplicationId = applicationId
            };
        }

        if (app.ApplicationStatus == ApplicationStatusConstants.Canceled)
        {
            return new ContractCancellationResultDto
            {
                Success = false,
                Message = "Hồ sơ đã được hủy trước đó.",
                ApplicationId = applicationId
            };
        }

        // Kiểm tra quyền: Chủ sở hữu hồ sơ (tự nguyện làm đơn) hoặc CĐT/Staff (cưỡng chế/duyệt)
        var caller = await _db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        var isOwner = app.ApplicantId == userId;
        var isStaff = caller?.Role != null && (
            caller.Role.RoleName == RoleConstants.HousingDeveloper ||
            caller.Role.RoleName == RoleConstants.DepartmentOfConstruction ||
            caller.Role.RoleName == RoleConstants.SystemAdministrator
        );

        if (!isOwner && !isStaff)
        {
            return new ContractCancellationResultDto
            {
                Success = false,
                Message = "Bạn không có quyền thực hiện hủy hợp đồng cho hồ sơ này.",
                ApplicationId = applicationId
            };
        }

        var preview = await PreviewContractCancellationAsync(applicationId);
        if (!preview.CanCancel)
        {
            return new ContractCancellationResultDto
            {
                Success = false,
                Message = preview.Message ?? "Hồ sơ không ở trạng thái hợp lệ để hủy.",
                ApplicationId = applicationId
            };
        }

        var isForced = dto.IsForcedRevocation || preview.IsEligibleForForcedRevocation;
        var oldStatus = app.ApplicationStatus;
        var releasedApartmentId = app.ApartmentId ?? app.Apartment?.Id;

        app.ApplicationStatus = ApplicationStatusConstants.Canceled;
        app.UpdatedAt = DateTime.UtcNow;

        // Giải phóng căn hộ
        if (app.Apartment != null || (app.ApartmentId.HasValue && app.ApartmentId.Value != Guid.Empty))
        {
            var apartmentId = app.ApartmentId ?? app.Apartment?.Id;
            var apartment = app.Apartment ?? (apartmentId.HasValue ? await _db.Apartments.FirstOrDefaultAsync(a => a.Id == apartmentId.Value) : null);
            if (apartment != null)
            {
                apartment.Status = "AVAILABLE";
                apartment.UpdatedAt = DateTime.UtcNow;
            }
            app.ApartmentId = null;
        }

        // Hoàn trả suất khả dụng dự án
        if (app.HousingProject != null)
        {
            app.HousingProject.AvailableUnits += 1;
            app.HousingProject.UpdatedAt = DateTime.UtcNow;
        }

        // Hủy tất cả đợt thu chưa đóng
        var unpaidInstallments = await _db.PaymentInstallments
            .Where(i => i.ApplicationId == applicationId && i.Status != InstallmentStatusConstants.Paid)
            .ToListAsync();

        foreach (var inst in unpaidInstallments)
        {
            inst.Status = InstallmentStatusConstants.Cancelled;
            inst.UpdatedAt = DateTime.UtcNow;
        }

        // Ghi Lịch sử Status History
        var actionName = isForced ? "FORCED_CONTRACT_REVOCATION" : "VOLUNTARY_CONTRACT_CANCELLATION";
        _db.Set<ApplicationStatusHistory>().Add(new ApplicationStatusHistory
        {
            HistoryId = Guid.NewGuid(),
            ApplicationId = applicationId,
            ChangedBy = userId,
            OldStatus = oldStatus,
            NewStatus = ApplicationStatusConstants.Canceled,
            Action = actionName,
            Note = $"{(isForced ? "Cưỡng chế thu hồi căn (quá đợt trễ hạn)" : "Tự nguyện hủy HĐ")}. Lý do: {dto.Reason}. Cọc Đợt 1 phạt tịch thu: {preview.DepositForfeited:N0} VND. Đợt 2+ đã đóng: {preview.Phase2PlusPaidAmount:N0} VND. Lãi phạt trễ hạn đã trừ: {preview.TotalUnpaidPenalty:N0} VND. Tiền thực hoàn: {preview.RefundAmount:N0} VND.",
            ChangedAt = DateTime.UtcNow
        });

        // Tự động đôn ứng viên tiếp theo từ Danh sách chờ (Waitlist) nếu có
        Guid? promotedApplicantId = null;
        string? promotedApplicantName = null;

        var nextWaitlistCandidate = await _db.HousingApplications
            .Include(a => a.Applicant)
            .Where(a => a.ProjectId == app.ProjectId
                     && a.WaitlistNumber.HasValue
                     && a.WaitlistNumber > 0
                     && a.ApplicationStatus != ApplicationStatusConstants.Canceled
                     && a.ApplicationStatus != ApplicationStatusConstants.Rejected)
            .OrderBy(a => a.WaitlistNumber)
            .FirstOrDefaultAsync();

        if (nextWaitlistCandidate != null)
        {
            promotedApplicantId = nextWaitlistCandidate.ApplicantId;
            promotedApplicantName = nextWaitlistCandidate.FullName ?? nextWaitlistCandidate.Applicant?.FullName;

            nextWaitlistCandidate.ApplicationStatus = ApplicationStatusConstants.Approved;
            nextWaitlistCandidate.WaitlistPromotedAt = DateTime.UtcNow;
            nextWaitlistCandidate.DepositDeadline = DateTime.UtcNow.AddHours(48); // 48 giờ để xác nhận nộp cọc
            nextWaitlistCandidate.LotteryResult = LotteryResultConstants.Won;
            nextWaitlistCandidate.UpdatedAt = DateTime.UtcNow;

            if (releasedApartmentId.HasValue && releasedApartmentId.Value != Guid.Empty)
            {
                nextWaitlistCandidate.ApartmentId = releasedApartmentId.Value;
                var ap = await _db.Apartments.FirstOrDefaultAsync(a => a.Id == releasedApartmentId.Value);
                if (ap != null)
                {
                    ap.Status = "ASSIGNED";
                    ap.UpdatedAt = DateTime.UtcNow;
                }
            }

            if (app.HousingProject != null && app.HousingProject.AvailableUnits > 0)
            {
                app.HousingProject.AvailableUnits -= 1;
            }

            _db.Set<ApplicationStatusHistory>().Add(new ApplicationStatusHistory
            {
                HistoryId = Guid.NewGuid(),
                ApplicationId = nextWaitlistCandidate.ApplicationId,
                ChangedBy = userId,
                OldStatus = "WAITLIST",
                NewStatus = ApplicationStatusConstants.Approved,
                Action = "PROMOTED_FROM_WAITLIST",
                Note = $"Được đôn từ Danh sách chờ (Waitlist #{nextWaitlistCandidate.WaitlistNumber}) lên trúng tuyển do căn hộ bị thu hồi từ hồ sơ {applicationId}.",
                ChangedAt = DateTime.UtcNow
            });

            try
            {
                await _notificationService.SendAsync(
                    nextWaitlistCandidate.ApplicantId,
                    "🎉 Chúc mừng! Được đôn từ Danh sách chờ (Waitlist)",
                    $"Bạn đã được đôn từ Danh sách chờ lên trúng tuyển căn hộ do có căn bị thu hồi. Vui lòng hoàn tất thanh toán cọc trong vòng 48 giờ (trước {nextWaitlistCandidate.DepositDeadline:dd/MM/yyyy HH:mm}).",
                    NotificationTypeConstants.ApplicationApproved);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send waitlist promotion notification for app {AppId}", nextWaitlistCandidate.ApplicationId);
            }
        }

        await _db.SaveChangesAsync();

        try
        {
            await _notificationService.SendAsync(
                app.ApplicantId,
                isForced ? "⚠️ Thu hồi căn hộ & Hủy hợp đồng" : "❌ Hợp đồng đã bị hủy / Phạt cọc",
                $"Hợp đồng mua bán căn hộ của bạn đã bị hủy. Tiền cọc Đợt 1 ({preview.DepositForfeited:N0} VND) bị tịch thu theo quy định. Tiền thực hoàn: {preview.RefundAmount:N0} VND.",
                NotificationTypeConstants.ApplicationCanceled);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send cancellation notification for app {AppId}", applicationId);
        }

        _logger.LogInformation(
            "Contract CANCELLED for App={AppId}. Forced={Forced}, Deposit Forfeited={Forfeited}, Refund={Refund}, PromotedWaitlist={Promoted}.",
            applicationId, isForced, preview.DepositForfeited, preview.RefundAmount, promotedApplicantName);

        return new ContractCancellationResultDto
        {
            Success = true,
            Message = $"{(isForced ? "Cưỡng chế thu hồi căn" : "Hủy hợp đồng")} thành công. Số tiền cọc bị tịch thu: {preview.DepositForfeited:N0} VND. Số tiền thực hoàn: {preview.RefundAmount:N0} VND."
                + (!string.IsNullOrEmpty(promotedApplicantName) ? $" Đã tự động đôn ứng viên {promotedApplicantName} từ Waitlist lên nhận căn." : ""),
            ApplicationId = applicationId,
            IsForcedRevocation = isForced,
            DepositForfeited = preview.DepositForfeited,
            RefundAmount = preview.RefundAmount,
            TotalPenaltyDeducted = preview.TotalUnpaidPenalty,
            PromotedWaitlistApplicantId = promotedApplicantId,
            PromotedWaitlistApplicantName = promotedApplicantName,
            CancelledAt = DateTime.UtcNow
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 7. Báo cáo tiến độ thu tiền & nợ phạt cho Chủ đầu tư / SXD
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<ProjectPaymentProgressDto> GetProjectPaymentProgressAsync(Guid projectId)
    {
        var project = await _db.HousingProjects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId)
            ?? throw new InvalidOperationException($"Dự án {projectId} không tồn tại.");

        var applications = await _db.HousingApplications
            .AsNoTracking()
            .Include(a => a.Apartment)
            .Where(a => a.ProjectId == projectId && a.ApplicationStatus != ApplicationStatusConstants.Draft)
            .ToListAsync();

        var appIds = applications.Select(a => a.ApplicationId).ToList();

        var allInstallments = await _db.PaymentInstallments
            .AsNoTracking()
            .Include(i => i.Milestone)
            .Where(i => appIds.Contains(i.ApplicationId))
            .ToListAsync();

        var installmentsByApp = allInstallments
            .GroupBy(i => i.ApplicationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var now = DateTime.UtcNow;
        const decimal dailyRate = 0.0005m;
        var items = new List<ApplicationProgressItemDto>();

        decimal totalExpected = 0m;
        decimal totalCollected = 0m;
        decimal totalOverdue = 0m;
        decimal totalPenalties = 0m;

        foreach (var app in applications)
        {
            var insts = installmentsByApp.TryGetValue(app.ApplicationId, out var list) ? list : new List<PaymentInstallment>();

            decimal appPaid = 0m;
            decimal appRemaining = 0m;
            decimal appPenalty = 0m;
            decimal appOverdue = 0m;
            decimal appTotalContract = app.Apartment?.Price ?? insts.Sum(i => i.Amount);

            int paidCount = 0;
            int overdueCount = 0;

            foreach (var i in insts)
            {
                if (i.Status == InstallmentStatusConstants.Paid)
                {
                    appPaid += i.Amount;
                    paidCount++;
                }
                else if (i.Status != InstallmentStatusConstants.Cancelled)
                {
                    appRemaining += i.Amount;

                    if (i.Status == InstallmentStatusConstants.Overdue || (i.Status == InstallmentStatusConstants.Pending && now > i.DueDate))
                    {
                        overdueCount++;
                        appOverdue += i.Amount;

                        var days = (int)Math.Floor((now - i.DueDate).TotalDays);
                        if (days > 0)
                        {
                            var pen = Math.Round(i.Amount * dailyRate * days, 0, MidpointRounding.AwayFromZero);
                            appPenalty += pen;
                        }
                    }
                }
            }

            totalExpected += appTotalContract;
            totalCollected += appPaid;
            totalOverdue += appOverdue;
            totalPenalties += appPenalty;

            items.Add(new ApplicationProgressItemDto
            {
                ApplicationId = app.ApplicationId,
                ApplicantName = app.FullName,
                CitizenId = app.CitizenId,
                SlotCode = app.SlotCode,
                ApartmentUnitName = app.Apartment?.UnitName,
                TotalContractAmount = appTotalContract,
                PaidAmount = appPaid,
                RemainingAmount = appRemaining,
                AccruedPenalty = appPenalty,
                PaidPhasesCount = paidCount,
                OverduePhasesCount = overdueCount,
                ApplicationStatus = app.ApplicationStatus
            });
        }

        double collectionRate = totalExpected > 0 ? Math.Round((double)(totalCollected / totalExpected * 100m), 2) : 0;

        return new ProjectPaymentProgressDto
        {
            ProjectId = projectId,
            ProjectName = project.ProjectName,
            TotalApplications = applications.Count,
            TotalExpectedAmount = totalExpected,
            TotalCollectedAmount = totalCollected,
            TotalOverdueAmount = totalOverdue,
            TotalAccruedPenalties = totalPenalties,
            CollectionRatePercentage = collectionRate,
            Items = items
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Private helpers
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<int> UnlockPhaseByEventAsync(Guid projectId, string triggerEvent)
    {
        _logger.LogInformation("UnlockPhaseByEvent: Project={ProjectId}, Event={TriggerEvent}", projectId, triggerEvent);

        var milestones = await _db.PaymentMilestones
            .Where(m => m.ProjectId == projectId && m.TriggerEvent == triggerEvent && m.IsActive)
            .ToListAsync();

        if (milestones.Count == 0) return 0;

        var milestoneIds = milestones.Select(m => m.Id).ToList();

        var lockedInstallments = await _db.PaymentInstallments
            .Include(i => i.HousingApplication)
            .Include(i => i.Milestone)
            .Where(i => milestoneIds.Contains(i.MilestoneId)
                        && i.Status == InstallmentStatusConstants.Locked
                        && i.HousingApplication.ProjectId == projectId)
            .ToListAsync();

        int unlockedCount = 0;
        var now = DateTime.UtcNow;

        foreach (var inst in lockedInstallments)
        {
            var prevPhaseOrder = inst.Milestone.PhaseOrder - 1;
            var prevPaid = prevPhaseOrder < 1 || await _db.PaymentInstallments
                .Include(i => i.Milestone)
                .AnyAsync(i => i.ApplicationId == inst.ApplicationId
                            && i.Milestone.PhaseOrder == prevPhaseOrder
                            && i.Status == InstallmentStatusConstants.Paid);

            if (prevPaid)
            {
                inst.Status = InstallmentStatusConstants.Pending;
                inst.StartDate = now;
                inst.DueDate = now.AddDays(inst.Milestone.DueDays);
                inst.UpdatedAt = now;
                unlockedCount++;

                try
                {
                    var eventName = TriggerEventConstants.GetDisplayName(triggerEvent);
                    await _notificationService.SendAsync(
                        inst.HousingApplication.ApplicantId,
                        $"🔔 Đợt thanh toán mới: {inst.Milestone.PhaseName}",
                        $"Tiến độ dự án ({eventName}) đã được Chủ đầu tư kích hoạt. Số tiền: {inst.Amount:N0} VND. Hạn đóng: {inst.DueDate:dd/MM/yyyy}.",
                        NotificationTypeConstants.InstallmentCreated);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send notification for unlocked installment {Id}", inst.Id);
                }
            }
        }

        await _db.SaveChangesAsync();
        return unlockedCount;
    }

    /// <summary>
    /// Đảm bảo một hồ sơ đã được cấp căn có đầy đủ các đợt đóng tiền (từ 3 đến 6 đợt theo cấu hình dự án của CĐT).
    /// Tự động đồng bộ Đợt 1 sang PAID nếu đã cọc, và Đợt 2 sang PENDING nếu đã ký hợp đồng.
    /// </summary>
    private async Task EnsureInstallmentsForApplicationAsync(Guid applicationId)
    {
        var app = await _db.HousingApplications
            .Include(a => a.Apartment)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

        if (app == null)
            return;

        var apartment = app.Apartment;
        if (apartment == null && app.ApartmentId.HasValue)
        {
            apartment = await _db.Apartments.FirstOrDefaultAsync(a => a.Id == app.ApartmentId.Value);
            app.Apartment = apartment;
        }

        if (apartment == null)
        {
            _logger.LogWarning("EnsureInstallments: App {AppId} has no apartment assigned yet.", applicationId);
            return;
        }

        await EnsureDefaultMilestonesAsync(app.ProjectId);

        var milestones = await _db.PaymentMilestones
            .Where(m => m.ProjectId == app.ProjectId && m.IsActive)
            .OrderBy(m => m.PhaseOrder)
            .ToListAsync();

        if (milestones.Count < 3)
            return;

        var existingInstallments = await _db.PaymentInstallments
            .Include(i => i.Milestone)
            .Where(i => i.ApplicationId == applicationId)
            .OrderBy(i => i.Milestone.PhaseOrder)
            .ToListAsync();

        var now = DateTime.UtcNow;

        // Đã thanh toán Đợt 1 (cọc) nếu status từ CONTRACT_PENDING trở đi hoặc có Payment Success
        var isD1Paid = app.ApplicationStatus == ApplicationStatusConstants.ContractPending
                    || app.ApplicationStatus == ApplicationStatusConstants.ContractSigned
                    || app.ApplicationStatus == ApplicationStatusConstants.DepositPaid
                    || app.ApplicationStatus == ApplicationStatusConstants.InstallmentInProgress
                    || app.ApplicationStatus == ApplicationStatusConstants.FullyPaid
                    || await _db.Payments.AnyAsync(p => p.ApplicationId == applicationId
                                                       && (p.Status == "Success" || p.Status == "Paid"));

        // Đã ký Hợp đồng nếu status từ CONTRACT_SIGNED trở đi hoặc PrincipleAgreement đã ký
        var isContractSigned = app.ApplicationStatus == ApplicationStatusConstants.ContractSigned
                            || app.ApplicationStatus == ApplicationStatusConstants.InstallmentInProgress
                            || app.ApplicationStatus == ApplicationStatusConstants.FullyPaid
                            || await _db.PrincipleAgreements.AnyAsync(p => p.ApplicationId == applicationId && p.IsSigned);

        if (existingInstallments.Count == 0)
        {
            var newInstallments = new List<PaymentInstallment>();
            decimal runningTotal = 0m;

            for (int idx = 0; idx < milestones.Count; idx++)
            {
                var m = milestones[idx];
                decimal amount;

                if (idx == milestones.Count - 1)
                {
                    amount = Math.Max(0m, apartment.Price - runningTotal);
                }
                else
                {
                    var pct = m.Percentage ?? 0m;
                    amount = Math.Round(apartment.Price * pct / 100m, 0, MidpointRounding.AwayFromZero);
                    runningTotal += amount;
                }

                string status;
                DateTime? paidAt = null;

                if (m.PhaseOrder == 1)
                {
                    status = isD1Paid ? InstallmentStatusConstants.Paid : InstallmentStatusConstants.Pending;
                    if (isD1Paid) paidAt = app.UpdatedAt ?? now;
                }
                else if (m.PhaseOrder == 2)
                {
                    // Đợt 2 tự động mở khi đã ký hợp đồng
                    status = isContractSigned ? InstallmentStatusConstants.Pending : InstallmentStatusConstants.Locked;
                }
                else
                {
                    status = InstallmentStatusConstants.Locked;
                }

                var inst = new PaymentInstallment
                {
                    Id            = Guid.NewGuid(),
                    ApplicationId = applicationId,
                    MilestoneId   = m.Id,
                    Amount        = amount,
                    StartDate     = now,
                    DueDate       = now.AddDays(m.DueDays),
                    Status        = status,
                    PaidAt        = paidAt,
                    CreatedAt     = now
                };
                newInstallments.Add(inst);
            }

            _db.PaymentInstallments.AddRange(newInstallments);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Generated {Count} installments matching project milestones for Application={AppId}.", milestones.Count, applicationId);
        }
        else
        {
            // Tự động kiểm tra và đồng bộ trạng thái nếu thiếu
            bool modified = false;

            var d1 = existingInstallments.FirstOrDefault(i => i.Milestone.PhaseOrder == 1);
            if (d1 != null && isD1Paid && d1.Status != InstallmentStatusConstants.Paid)
            {
                d1.Status = InstallmentStatusConstants.Paid;
                d1.PaidAt ??= app.UpdatedAt ?? now;
                d1.UpdatedAt = now;
                modified = true;
            }

            var d2 = existingInstallments.FirstOrDefault(i => i.Milestone.PhaseOrder == 2);
            if (d2 != null && isContractSigned && d2.Status == InstallmentStatusConstants.Locked)
            {
                d2.Status = InstallmentStatusConstants.Pending;
                d2.StartDate = now;
                d2.DueDate = now.AddDays(d2.Milestone.DueDays);
                d2.UpdatedAt = now;
                modified = true;
            }

            if (modified)
            {
                await _db.SaveChangesAsync();
                _logger.LogInformation("Self-healed D1/D2 installments for Application={AppId}.", applicationId);
            }
        }
    }

    /// <summary>
    /// Thuật toán chia số tiền 6 đợt không bị lệch lẻ xu:
    /// Đợt 1 = 10%, Đợt 2 = 20%, Đợt 3 = 20%, Đợt 4 = 20%, Đợt 5 = 25% (+ 2% Phí bảo trì), Đợt 6 = 5% (nhận phần dư).
    /// </summary>
    public static (decimal d1, decimal d2, decimal d3, decimal d4, decimal d5, decimal d6) Calculate6PhaseAmounts(decimal price)
    {
        var d1 = Math.Floor(price * 0.10m);
        var d2 = Math.Floor(price * 0.20m);
        var d3 = Math.Floor(price * 0.20m);
        var d4 = Math.Floor(price * 0.20m);
        var d5Base = Math.Floor(price * 0.25m);
        var pbt = Math.Round(price * 0.02m, 0, MidpointRounding.AwayFromZero);
        var d5 = d5Base + pbt;
        var d6 = price - (d1 + d2 + d3 + d4 + d5Base);
        if (d6 < 0) d6 = 0;

        return (d1, d2, d3, d4, d5, d6);
    }

    /// <summary>
    /// Đảm bảo dự án có ít nhất các đợt đóng tiền mặc định nếu CĐT chưa cấu hình.
    /// Nếu CĐT đã cấu hình từ 3 đến 6 đợt -> giữ nguyên cấu hình của CĐT.
    /// </summary>
    private async Task EnsureDefaultMilestonesAsync(Guid projectId)
    {
        var allMilestones = await _db.PaymentMilestones
            .Where(m => m.ProjectId == projectId && m.IsActive)
            .ToListAsync();

        if (allMilestones.Count > 0)
        {
            // Dự án đã được CĐT cấu hình từ 3 đến 6 đợt -> Giữ nguyên cấu hình CĐT
            return;
        }

        var standardConfigs = new (int PhaseOrder, string PhaseName, decimal Pct, string Trigger, int DueDays, string Desc)[]
        {
            (1, "Đợt 1", 10m, TriggerEventConstants.OnLotteryWon, 7, "Đợt 1 — 10% giá trị căn hộ khi trúng bốc thăm / cấp nhà"),
            (2, "Đợt 2", 20m, TriggerEventConstants.OnContractSigned, 15, "Đợt 2 — 20% giá trị căn hộ khi ký Hợp đồng mua bán chính thức"),
            (3, "Đợt 3", 20m, TriggerEventConstants.ConstructionRoughFloor, 30, "Đợt 3 — 20% giá trị căn hộ khi hoàn thành xây thô"),
            (4, "Đợt 4", 20m, TriggerEventConstants.RoofingCompleted, 30, "Đợt 4 — 20% giá trị căn hộ khi cất nóc tòa nhà"),
            (5, "Đợt 5", 25m, TriggerEventConstants.Handover, 30, "Đợt 5 — 25% giá trị căn hộ (+ 2% Phí bảo trì) khi bàn giao nhà & chìa khóa"),
            (6, "Đợt 6", 5m, TriggerEventConstants.RedBookIssued, 30, "Đợt 6 — 5% phần còn lại khi nhận Giấy chứng nhận (Sổ hồng)")
        };

        var now = DateTime.UtcNow;
        var newMilestones = standardConfigs.Select(c => new PaymentMilestone
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            PhaseOrder = c.PhaseOrder,
            PhaseName = c.PhaseName,
            CalculationType = CalculationTypeConstants.Percentage,
            Percentage = c.Pct,
            TriggerEvent = c.Trigger,
            DueDays = c.DueDays,
            Description = c.Desc,
            IsActive = true,
            CreatedAt = now
        }).ToList();

        _db.PaymentMilestones.AddRange(newMilestones);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Seeded default 6-phase payment milestones for Project={ProjectId}.", projectId);
    }

    /// <summary>
    /// Chia đợt % trên giá căn: các đợt trước làm tròn VNĐ;
    /// đợt % cuối (theo PhaseOrder trong batch mới) nhận phần dư để
    /// tổng các đợt % = Round(giá × tổng%).
    /// </summary>
    private static void ApplyPercentageRemainder(
        decimal apartmentPrice,
        IReadOnlyList<PaymentInstallment> existingPctInstallments,
        IReadOnlyList<(PaymentMilestone Milestone, PaymentInstallment Installment)> newPct)
    {
        if (newPct.Count == 0)
            return;

        foreach (var (m, _) in newPct)
        {
            if (!m.Percentage.HasValue)
                throw new InvalidOperationException(
                    $"Milestone '{m.PhaseName}' dùng PERCENTAGE nhưng Percentage chưa được cấu hình.");
        }

        var totalPct = existingPctInstallments.Sum(e => e.Milestone.Percentage ?? 0m)
                       + newPct.Sum(x => x.Milestone.Percentage!.Value);

        var targetTotal = Math.Round(
            apartmentPrice * totalPct / 100m,
            0,
            MidpointRounding.AwayFromZero);

        var allocated = existingPctInstallments.Sum(e => e.Amount);

        for (var i = 0; i < newPct.Count; i++)
        {
            var (milestone, installment) = newPct[i];
            var isLast = i == newPct.Count - 1;

            if (!isLast)
            {
                var amount = Math.Round(
                    apartmentPrice * milestone.Percentage!.Value / 100m,
                    0,
                    MidpointRounding.AwayFromZero);
                if (amount < 0) amount = 0;
                installment.Amount = amount;
                allocated += amount;
            }
            else
            {
                // Đợt cuối ăn phần dư (có thể ±1–2đ so với Round riêng từng đợt)
                var remainder = targetTotal - allocated;
                installment.Amount = remainder < 0 ? 0 : remainder;
            }
        }
    }

    private static decimal CalculateAmount(PaymentMilestone milestone, Domain.Entities.Apartment? apartment)
    {
        return milestone.CalculationType switch
        {
            CalculationTypeConstants.FixedAmount =>
                milestone.FixedAmount
                ?? throw new InvalidOperationException(
                    $"Milestone '{milestone.PhaseName}' (PhaseOrder={milestone.PhaseOrder}) "
                    + "dùng FIXED_AMOUNT nhưng FixedAmount chưa được cấu hình."),

            // PERCENTAGE đơn lẻ (fallback) — batch chính dùng ApplyPercentageRemainder
            CalculationTypeConstants.Percentage =>
                (apartment != null && milestone.Percentage.HasValue)
                    ? Math.Round(
                        apartment.Price * milestone.Percentage.Value / 100m,
                        0,
                        MidpointRounding.AwayFromZero)
                    : throw new InvalidOperationException(
                        $"Milestone '{milestone.PhaseName}' (PhaseOrder={milestone.PhaseOrder}) "
                        + "dùng PERCENTAGE nhưng thiếu Apartment hoặc Percentage."),

            _ => throw new InvalidOperationException(
                $"CalculationType không hợp lệ: '{milestone.CalculationType}' "
                + $"cho milestone '{milestone.PhaseName}'.")
        };
    }

    private static string GenerateOrderId()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var random = new Random().Next(1000, 9999);
        return $"{timestamp}{random}";
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();

        foreach (var c in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString()
            .Normalize(System.Text.NormalizationForm.FormC)
            .Replace("đ", "d")
            .Replace("Đ", "D");
    }

    private static PaymentResponseDto Fail(string message) => new()
    {
        Success = false,
        Message = message
    };
}
