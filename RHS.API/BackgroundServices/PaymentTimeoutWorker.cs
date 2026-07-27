using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Application.Interfaces;
using RHS.Infrastructure.Data;

namespace RHS.API.BackgroundServices;

/// <summary>
/// Hết hạn theo luồng chuẩn:
/// - CONTRACT_PENDING quá hạn ký HĐ → EXPIRED
/// - CONTRACT_SIGNED quá hạn đặt cọc (từ SignedAt) → EXPIRED
/// Không expire APPROVED: sau duyệt còn chờ CĐT chốt / bốc thăm, chưa đến bước cọc.
/// </summary>
public class PaymentTimeoutWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentTimeoutWorker> _logger;
    private readonly TimeSpan _period = TimeSpan.FromMinutes(10);

    public PaymentTimeoutWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PaymentTimeoutWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PaymentTimeoutWorker started.");

        using var timer = new PeriodicTimer(_period);
        while (await timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredApplicationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in PaymentTimeoutWorker.");
            }
        }
    }

    private async Task ProcessExpiredApplicationsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var policyService = scope.ServiceProvider.GetRequiredService<IPolicyService>();

        var depositHours = await policyService.GetValueAsync(PolicyKeys.DepositPaymentHours, 24, stoppingToken);
        var contractDays = await policyService.GetValueAsync(PolicyKeys.ContractSigningDeadlineDays, 15, stoppingToken);

        var depositCutoff = DateTime.UtcNow.AddHours(-depositHours);
        var contractCutoff = DateTime.UtcNow.AddDays(-contractDays);

        // Quá hạn ký HĐ nguyên tắc (sau khi được chốt danh sách / trúng bốc thăm)
        var pendingSignExpired = await context.HousingApplications
            .Where(x =>
                x.ApplicationStatus == ApplicationStatusConstants.ContractPending &&
                (x.UpdatedAt ?? x.SubmittedAt) < contractCutoff)
            .ToListAsync(stoppingToken);

        // Quá hạn đặt cọc — chỉ sau khi đã ký HĐ (mốc SignedAt)
        var signedUnpaidExpired = await context.HousingApplications
            .Where(x =>
                x.ApplicationStatus == ApplicationStatusConstants.ContractSigned &&
                x.PrincipleAgreement != null &&
                x.PrincipleAgreement.IsSigned &&
                x.PrincipleAgreement.SignedAt.HasValue &&
                x.PrincipleAgreement.SignedAt.Value < depositCutoff)
            .ToListAsync(stoppingToken);

        if (pendingSignExpired.Count == 0 && signedUnpaidExpired.Count == 0)
            return;

        _logger.LogInformation(
            "Timeout candidates: {PendingCount} CONTRACT_PENDING (>{Days}d), {SignedCount} CONTRACT_SIGNED unpaid (>{Hours}h).",
            pendingSignExpired.Count, contractDays, signedUnpaidExpired.Count, depositHours);

        foreach (var app in pendingSignExpired)
        {
            await ExpireAsync(
                context,
                notificationService,
                app,
                action: ReviewActionConstants.PaymentTimeout,
                note: $"Tự động hủy do quá hạn ký hợp đồng nguyên tắc ({contractDays} ngày — PolicyConfig CONTRACT_SIGNING_DEADLINE_DAYS).",
                notifTitle: "Hồ sơ đã hết hạn ký hợp đồng",
                notifBody: $"Hồ sơ của bạn đã bị hủy do không ký hợp đồng nguyên tắc trong vòng {contractDays} ngày.",
                stoppingToken);
        }

        foreach (var app in signedUnpaidExpired)
        {
            var isPaid = await context.Payments.AnyAsync(p =>
                p.ApplicationId == app.ApplicationId &&
                p.Status == "Success",
                stoppingToken);

            if (isPaid)
                continue;

            await ExpireAsync(
                context,
                notificationService,
                app,
                action: ReviewActionConstants.PaymentTimeout,
                note: $"Tự động hủy do quá hạn thanh toán đặt cọc sau khi ký HĐ ({depositHours} giờ — PolicyConfig DEPOSIT_PAYMENT_HOURS).",
                notifTitle: "Hồ sơ đã hết hạn thanh toán",
                notifBody: $"Hồ sơ của bạn đã bị hủy do không thanh toán đặt cọc trong vòng {depositHours} giờ sau khi ký hợp đồng nguyên tắc.",
                stoppingToken);
        }
    }

    private async Task ExpireAsync(
        AppDbContext context,
        INotificationService notificationService,
        HousingApplication app,
        string action,
        string note,
        string notifTitle,
        string notifBody,
        CancellationToken stoppingToken)
    {
        // Tránh race nếu status đã đổi giữa lúc query và xử lý
        if (app.ApplicationStatus is not (
            ApplicationStatusConstants.ContractPending or
            ApplicationStatusConstants.ContractSigned))
        {
            return;
        }

        _logger.LogInformation(
            "Expiring application {AppId} from {Status}.",
            app.ApplicationId, app.ApplicationStatus);

        await using var transaction = await context.Database.BeginTransactionAsync(stoppingToken);
        try
        {
            var oldStatus = app.ApplicationStatus;
            app.ApplicationStatus = ApplicationStatusConstants.Expired;
            app.UpdatedAt = DateTime.UtcNow;
            context.HousingApplications.Update(app);

            context.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                HistoryId = Guid.NewGuid(),
                ApplicationId = app.ApplicationId,
                ChangedBy = app.ApplicantId,
                Action = action,
                OldStatus = oldStatus,
                NewStatus = ApplicationStatusConstants.Expired,
                Note = note,
                ChangedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync(stoppingToken);
            await transaction.CommitAsync(stoppingToken);

            try
            {
                await notificationService.SendAsync(
                    app.ApplicantId,
                    notifTitle,
                    notifBody,
                    NotificationTypeConstants.ApplicationExpired);
            }
            catch (Exception notifEx)
            {
                _logger.LogWarning(notifEx, "Failed to send expiry notification for AppId {AppId}.", app.ApplicationId);
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(stoppingToken);
            _logger.LogError(ex, "Failed to expire application {AppId}.", app.ApplicationId);
        }
    }
}
