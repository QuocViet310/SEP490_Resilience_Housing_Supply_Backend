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

        // Trường hợp 1: Khi trúng bốc thăm / cấp nhà (ON_LOTTERY_WON) → Sinh toàn bộ 6 đợt (Đợt 1 PENDING, Đợt 2-6 LOCKED)
        if (string.Equals(triggerEvent, TriggerEventConstants.OnLotteryWon, StringComparison.OrdinalIgnoreCase))
        {
            var milestones = await _db.PaymentMilestones
                .Where(m => m.ProjectId == app.ProjectId && m.IsActive)
                .OrderBy(m => m.PhaseOrder)
                .ToListAsync();

            var existingMilestoneIds = (await _db.PaymentInstallments
                .Where(i => i.ApplicationId == applicationId)
                .Select(i => i.MilestoneId)
                .ToListAsync())
                .ToHashSet();

            var milestonesToCreate = milestones
                .Where(m => !existingMilestoneIds.Contains(m.Id))
                .OrderBy(m => m.PhaseOrder)
                .ToList();

            if (milestonesToCreate.Count == 0)
                return;

            if (app.Apartment == null)
            {
                _logger.LogWarning("App {AppId} hasn't been assigned an Apartment yet. Skipping installment generation.", applicationId);
                return;
            }

            var (a1, a2, a3, a4, a5, a6) = Calculate6PhaseAmounts(app.Apartment.Price);
            var amountsByPhase = new Dictionary<int, decimal>
            {
                [1] = a1,
                [2] = a2,
                [3] = a3,
                [4] = a4,
                [5] = a5,
                [6] = a6
            };

            var newInstallments = new List<PaymentInstallment>();
            foreach (var m in milestonesToCreate)
            {
                var amount = amountsByPhase.TryGetValue(m.PhaseOrder, out var amt)
                    ? amt
                    : CalculateAmount(m, app.Apartment);

                // Đợt 1: PENDING; Đợt 2-6: LOCKED
                var initialStatus = m.PhaseOrder == 1
                    ? InstallmentStatusConstants.Pending
                    : InstallmentStatusConstants.Locked;

                var inst = new PaymentInstallment
                {
                    Id            = Guid.NewGuid(),
                    ApplicationId = applicationId,
                    MilestoneId   = m.Id,
                    Amount        = amount,
                    StartDate     = eventDate,
                    DueDate       = eventDate.AddDays(m.DueDays),
                    Status        = initialStatus,
                    CreatedAt     = DateTime.UtcNow
                };
                newInstallments.Add(inst);
            }

            if (newInstallments.Count > 0)
            {
                _db.PaymentInstallments.AddRange(newInstallments);

                // Cập nhật trạng thái hồ sơ sang DEPOSIT_PENDING nếu đang APPROVED
                if (app.ApplicationStatus == ApplicationStatusConstants.Approved
                    || app.ApplicationStatus == ApplicationStatusConstants.ApprovedByTimeout)
                {
                    app.ApplicationStatus = ApplicationStatusConstants.DepositPending;
                    app.UpdatedAt = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync();

                // Gửi thông báo cho Đợt 1 (Cọc)
                var d1Inst = newInstallments.FirstOrDefault(i => i.Status == InstallmentStatusConstants.Pending);
                if (d1Inst != null)
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
            }
            return;
        }

        // Trường hợp 2: Khi Ký Hợp đồng (ON_CONTRACT_SIGNED) → Unlock Đợt 2 (LOCKED → PENDING)
        if (string.Equals(triggerEvent, TriggerEventConstants.OnContractSigned, StringComparison.OrdinalIgnoreCase))
        {
            var d2Inst = await _db.PaymentInstallments
                .Include(i => i.Milestone)
                .FirstOrDefaultAsync(i => i.ApplicationId == applicationId
                                          && i.Milestone.PhaseOrder == 2
                                          && i.Status == InstallmentStatusConstants.Locked);

            if (d2Inst != null)
            {
                d2Inst.Status = InstallmentStatusConstants.Pending;
                d2Inst.StartDate = eventDate;
                d2Inst.DueDate = eventDate.AddDays(d2Inst.Milestone.DueDays);
                d2Inst.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                try
                {
                    await _notificationService.SendAsync(
                        app.ApplicantId,
                        "📝 Ký hợp đồng thành công - Thông báo Đợt 2",
                        $"Ký Hợp đồng thành công. Khoản Đợt 2: {d2Inst.Amount:N0} VND. Hạn đóng: {d2Inst.DueDate:dd/MM/yyyy}.",
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
        // Self-heal: payment đã Paid nhưng installment còn PENDING (lỗi history ChangedBy)
        await HealPaidInstallmentsForApplicationAsync(applicationId);

        var app = await _db.HousingApplications
            .AsNoTracking()
            .Include(a => a.Apartment)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

        if (app == null) return null;

        var installments = await _db.PaymentInstallments
            .AsNoTracking()
            .Include(i => i.Milestone)
            .Where(i => i.ApplicationId == applicationId)
            .OrderBy(i => i.Milestone.PhaseOrder)
            .ToListAsync();

        var now = DateTime.UtcNow;

        var phases = installments.Select(i => new InstallmentDto
        {
            Id            = i.Id,
            PhaseOrder    = i.Milestone.PhaseOrder,
            PhaseName     = i.Milestone.PhaseName,
            Amount        = i.Amount,
            StartDate     = i.StartDate,
            DueDate       = i.DueDate,
            Status        = i.Status,
            PaidAt        = i.PaidAt,
            RemainingDays = (int)(i.DueDate - now).TotalDays,
            Note          = i.Note
        }).ToList();

        return new InstallmentSummaryDto
        {
            ApplicationId     = applicationId,
            ApartmentTypeName = app.Apartment?.UnitName, // legacy field name = tên căn đã bàn giao
            ApartmentArea     = app.Apartment?.Area,
            ApartmentPrice    = app.Apartment?.Price,
            TotalAmount       = phases.Sum(p => p.Amount),
            TotalPaid         = phases.Where(p => p.Status == InstallmentStatusConstants.Paid).Sum(p => p.Amount),
            TotalRemaining    = phases.Where(p => p.Status != InstallmentStatusConstants.Paid
                                                && p.Status != InstallmentStatusConstants.Cancelled)
                                      .Sum(p => p.Amount),
            TotalPhases       = phases.Count,
            PaidPhases        = phases.Count(p => p.Status == InstallmentStatusConstants.Paid),
            Phases            = phases
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

        // Tạo Payment record
        var orderId = GenerateOrderId();
        var projectName = RemoveDiacritics(
            installment.HousingApplication.HousingProject.ProjectName);
        var orderInfo = $"TT {installment.Milestone.PhaseName} - {orderId} - {projectName} - InstId:{installmentId}";

        var payment = new Payment
        {
            Id               = Guid.NewGuid(),
            UserId           = userId,
            ApplicationId    = installment.ApplicationId,
            HousingProjectId = installment.HousingApplication.ProjectId,
            OrderId          = orderId,
            OrderInfo        = orderInfo,
            Amount           = installment.Amount,
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
            Amount      = installment.Amount,
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

    // ═══════════════════════════════════════════════════════════════════════
    // Private helpers
    // ═══════════════════════════════════════════════════════════════════════

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
    /// Đảm bảo dự án có đủ 6 milestones template chuẩn:
    /// Đợt 1 (10% - ON_LOTTERY_WON), Đợt 2 (20% - ON_CONTRACT_SIGNED),
    /// Đợt 3 (20% - CONSTRUCTION_ROUGH_FLOOR), Đợt 4 (20% - ROOFING_COMPLETED),
    /// Đợt 5 (25% - HANDOVER), Đợt 6 (5% - RED_BOOK_ISSUED).
    /// </summary>
    private async Task EnsureDefaultMilestonesAsync(Guid projectId)
    {
        var active = await _db.PaymentMilestones
            .Where(m => m.ProjectId == projectId && m.IsActive)
            .OrderBy(m => m.PhaseOrder)
            .ToListAsync();

        if (active.Count >= 6)
            return;

        // Vô hiệu hóa các template cũ chưa chuẩn 6 đợt
        foreach (var m in active)
            m.IsActive = false;

        await _db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        _db.PaymentMilestones.AddRange(
            new PaymentMilestone
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                PhaseOrder = 1,
                PhaseName = "Đợt 1",
                CalculationType = CalculationTypeConstants.Percentage,
                Percentage = 10m,
                TriggerEvent = TriggerEventConstants.OnLotteryWon,
                DueDays = 7,
                Description = "Đợt 1 — 10% giá trị căn hộ khi trúng bốc thăm / cấp nhà",
                IsActive = true,
                CreatedAt = now
            },
            new PaymentMilestone
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                PhaseOrder = 2,
                PhaseName = "Đợt 2",
                CalculationType = CalculationTypeConstants.Percentage,
                Percentage = 20m,
                TriggerEvent = TriggerEventConstants.OnContractSigned,
                DueDays = 15,
                Description = "Đợt 2 — 20% giá trị căn hộ khi ký Hợp đồng mua bán chính thức",
                IsActive = true,
                CreatedAt = now
            },
            new PaymentMilestone
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                PhaseOrder = 3,
                PhaseName = "Đợt 3",
                CalculationType = CalculationTypeConstants.Percentage,
                Percentage = 20m,
                TriggerEvent = TriggerEventConstants.ConstructionRoughFloor,
                DueDays = 30,
                Description = "Đợt 3 — 20% giá trị căn hộ khi hoàn thành xây thô",
                IsActive = true,
                CreatedAt = now
            },
            new PaymentMilestone
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                PhaseOrder = 4,
                PhaseName = "Đợt 4",
                CalculationType = CalculationTypeConstants.Percentage,
                Percentage = 20m,
                TriggerEvent = TriggerEventConstants.RoofingCompleted,
                DueDays = 30,
                Description = "Đợt 4 — 20% giá trị căn hộ khi cất nóc tòa nhà",
                IsActive = true,
                CreatedAt = now
            },
            new PaymentMilestone
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                PhaseOrder = 5,
                PhaseName = "Đợt 5",
                CalculationType = CalculationTypeConstants.Percentage,
                Percentage = 25m,
                TriggerEvent = TriggerEventConstants.Handover,
                DueDays = 30,
                Description = "Đợt 5 — 25% giá trị căn hộ (+ 2% Phí bảo trì) khi bàn giao nhà & chìa khóa",
                IsActive = true,
                CreatedAt = now
            },
            new PaymentMilestone
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                PhaseOrder = 6,
                PhaseName = "Đợt 6",
                CalculationType = CalculationTypeConstants.Percentage,
                Percentage = 5m,
                TriggerEvent = TriggerEventConstants.RedBookIssued,
                DueDays = 30,
                Description = "Đợt 6 — 5% phần còn lại khi nhận Giấy chứng nhận (Sổ hồng)",
                IsActive = true,
                CreatedAt = now
            });

        await _db.SaveChangesAsync();
        _logger.LogInformation(
            "Seeded default 6-phase payment milestones for Project={ProjectId}.", projectId);
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
