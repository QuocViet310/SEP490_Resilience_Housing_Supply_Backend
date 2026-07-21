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
    public async Task FireTriggerEventAsync(
        Guid applicationId, string triggerEvent, DateTime eventDate)
    {
        _logger.LogInformation(
            "FireTriggerEvent: App={AppId}, Event={Event}, Date={Date}.",
            applicationId, triggerEvent, eventDate);

        var app = await _db.HousingApplications
            .Include(a => a.ApartmentType)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId)
            ?? throw new InvalidOperationException($"Hồ sơ {applicationId} không tồn tại.");

        // Lấy milestones active phù hợp với trigger event
        var milestones = await _db.PaymentMilestones
            .Where(m => m.ProjectId == app.ProjectId
                     && m.TriggerEvent == triggerEvent
                     && m.IsActive)
            .OrderBy(m => m.PhaseOrder)
            .ToListAsync();

        if (milestones.Count == 0)
        {
            _logger.LogWarning(
                "No active milestones found for Project={ProjectId}, Event={Event}.",
                app.ProjectId, triggerEvent);
            return;
        }

        // Idempotency: bỏ qua milestones đã có installment
        var existingMilestoneIds = (await _db.PaymentInstallments
            .Where(i => i.ApplicationId == applicationId)
            .Select(i => i.MilestoneId)
            .ToListAsync())
            .ToHashSet();

        var newInstallments = new List<PaymentInstallment>();

        foreach (var milestone in milestones)
        {
            if (existingMilestoneIds.Contains(milestone.Id))
            {
                _logger.LogDebug(
                    "Skip duplicate: Milestone={MilestoneId} already has installment for App={AppId}.",
                    milestone.Id, applicationId);
                continue;
            }

            var amount = CalculateAmount(milestone, app.ApartmentType);
            var dueDate = eventDate.AddDays(milestone.DueDays);

            newInstallments.Add(new PaymentInstallment
            {
                Id            = Guid.NewGuid(),
                ApplicationId = applicationId,
                MilestoneId   = milestone.Id,
                Amount        = amount,
                StartDate     = eventDate,
                DueDate       = dueDate,
                Status        = InstallmentStatusConstants.Pending,
                CreatedAt     = DateTime.UtcNow
            });

            _logger.LogInformation(
                "Created installment: Phase={Phase}, Amount={Amount}, DueDate={Due}, App={AppId}.",
                milestone.PhaseName, amount, dueDate, applicationId);
        }

        if (newInstallments.Count > 0)
        {
            _db.PaymentInstallments.AddRange(newInstallments);
            await _db.SaveChangesAsync();

            // Gửi notification cho applicant
            foreach (var inst in newInstallments)
            {
                var milestone = milestones.First(m => m.Id == inst.MilestoneId);
                try
                {
                    await _notificationService.SendAsync(
                        app.ApplicantId,
                        $"Khoản thu mới: {milestone.PhaseName}",
                        $"Số tiền: {inst.Amount:N0} VND. Hạn thanh toán: {inst.DueDate:dd/MM/yyyy}.",
                        NotificationTypeConstants.InstallmentCreated);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to send notification for installment {Id}.", inst.Id);
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2. GetSummary — tổng hợp lịch đóng tiền theo hồ sơ
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<InstallmentSummaryDto?> GetSummaryAsync(Guid applicationId)
    {
        var app = await _db.HousingApplications
            .AsNoTracking()
            .Include(a => a.ApartmentType)
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
            ApartmentTypeName = app.ApartmentType?.TypeName,
            ApartmentArea     = app.ApartmentType?.Area,
            ApartmentPrice    = app.ApartmentType?.Price,
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

        // Kiểm tra không có payment Pending cho installment này
        var hasPending = await _db.Payments
            .AnyAsync(p => p.ApplicationId == installment.ApplicationId
                        && p.Status == "Pending"
                        && p.OrderInfo.Contains(installmentId.ToString()));

        if (hasPending)
            return Fail("Đã có giao dịch đang chờ xử lý cho khoản thu này.");

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
            CreatedDate = DateTime.Now
        };

        var paymentUrl = _vnPayService.CreatePaymentUrl(httpContext, vnpRequest);

        _logger.LogInformation(
            "Installment payment created: OrderId={OrderId}, InstallmentId={InstId}, Amount={Amount}.",
            orderId, installmentId, installment.Amount);

        return new PaymentResponseDto
        {
            Success    = true,
            Message    = "Tạo URL thanh toán thành công",
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

        installment.Status    = InstallmentStatusConstants.Paid;
        installment.PaidAt    = DateTime.UtcNow;
        installment.PaymentId = paymentId;
        installment.UpdatedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Installment PAID: Id={Id}, Phase={Phase}, Amount={Amount}, App={AppId}.",
            installmentId, installment.Milestone.PhaseName, installment.Amount,
            installment.ApplicationId);

        // Ghi lịch sử hồ sơ
        _db.Set<ApplicationStatusHistory>().Add(new ApplicationStatusHistory
        {
            HistoryId     = Guid.NewGuid(),
            ApplicationId = installment.ApplicationId,
            OldStatus     = installment.HousingApplication.ApplicationStatus,
            NewStatus     = installment.HousingApplication.ApplicationStatus, // chưa đổi status
            Action        = ReviewActionConstants.InstallmentPayment,
            Note          = $"Thanh toán {installment.Milestone.PhaseName}: {installment.Amount:N0} VND",
            ChangedAt     = DateTime.UtcNow
        });

        // Gửi notification
        try
        {
            await _notificationService.SendAsync(
                installment.HousingApplication.ApplicantId,
                $"✅ Thanh toán thành công: {installment.Milestone.PhaseName}",
                $"Đã thanh toán {installment.Amount:N0} VND cho {installment.Milestone.PhaseName}.",
                NotificationTypeConstants.InstallmentPaid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send paid notification for installment {Id}.", installmentId);
        }

        // Kiểm tra xem tất cả đợt đã PAID chưa → FULLY_PAID
        var allInstallments = await _db.PaymentInstallments
            .Where(i => i.ApplicationId == installment.ApplicationId)
            .ToListAsync();

        var allPaid = allInstallments.All(i =>
            i.Status == InstallmentStatusConstants.Paid
            || i.Status == InstallmentStatusConstants.Cancelled);

        if (allPaid && allInstallments.Count > 0)
        {
            var application = installment.HousingApplication;
            var oldStatus = application.ApplicationStatus;
            application.ApplicationStatus = ApplicationStatusConstants.FullyPaid;
            application.UpdatedAt = DateTime.UtcNow;

            _db.Set<ApplicationStatusHistory>().Add(new ApplicationStatusHistory
            {
                HistoryId     = Guid.NewGuid(),
                ApplicationId = application.ApplicationId,
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
                    application.ApplicantId,
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

    /// <summary>
    /// Tính số tiền dựa trên CalculationType của milestone.
    /// FIXED_AMOUNT: dùng FixedAmount trực tiếp.
    /// PERCENTAGE:   dùng Percentage × ApartmentType.Price.
    /// </summary>
    private static decimal CalculateAmount(PaymentMilestone milestone, ApartmentType? apartmentType)
    {
        return milestone.CalculationType switch
        {
            CalculationTypeConstants.FixedAmount =>
                milestone.FixedAmount
                ?? throw new InvalidOperationException(
                    $"Milestone '{milestone.PhaseName}' (PhaseOrder={milestone.PhaseOrder}) "
                    + "dùng FIXED_AMOUNT nhưng FixedAmount chưa được cấu hình."),

            CalculationTypeConstants.Percentage =>
                (apartmentType != null && milestone.Percentage.HasValue)
                    ? Math.Round(apartmentType.Price * milestone.Percentage.Value / 100m, 0)
                    : throw new InvalidOperationException(
                        $"Milestone '{milestone.PhaseName}' (PhaseOrder={milestone.PhaseOrder}) "
                        + "dùng PERCENTAGE nhưng thiếu ApartmentType hoặc Percentage."),

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
