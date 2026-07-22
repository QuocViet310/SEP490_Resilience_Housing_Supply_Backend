using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Application.Interfaces;
using RHS.Infrastructure.Data;

namespace RHS.API.BackgroundServices;

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

        var cutoffTime = DateTime.UtcNow.AddHours(-depositHours);
        var contractCutoffTime = DateTime.UtcNow.AddDays(-contractDays);

        var expiredApplications = await context.HousingApplications
            .Where(x => ((x.ApplicationStatus == ApplicationStatusConstants.Approved || x.ApplicationStatus == ApplicationStatusConstants.ApprovedByTimeout)
                         && x.FinalDecisionDate.HasValue && x.FinalDecisionDate.Value < cutoffTime)
                     || (x.ApplicationStatus == ApplicationStatusConstants.ContractPending
                         && (x.UpdatedAt ?? x.SubmittedAt) < contractCutoffTime))
            .ToListAsync(stoppingToken);

        if (!expiredApplications.Any())
            return;

        _logger.LogInformation(
            "Found {Count} potentially expired approved applications (deadline={Hours}h).",
            expiredApplications.Count, depositHours);

        foreach (var app in expiredApplications)
        {
            var isPaid = await context.Payments.AnyAsync(p =>
                p.ApplicationId == app.ApplicationId &&
                p.Status == "Success",
                stoppingToken);

            if (!isPaid)
            {
                _logger.LogInformation(
                    "Application {AppId} unpaid after {Hours}h. Expiring (no unit hold to release — Hướng A).",
                    app.ApplicationId, depositHours);

                using var transaction = await context.Database.BeginTransactionAsync(stoppingToken);
                try
                {
                    var oldStatus = app.ApplicationStatus;
                    app.ApplicationStatus = ApplicationStatusConstants.Expired;
                    app.UpdatedAt = DateTime.UtcNow;
                    context.HousingApplications.Update(app);

                    var history = new ApplicationStatusHistory
                    {
                        HistoryId = Guid.NewGuid(),
                        ApplicationId = app.ApplicationId,
                        ChangedBy = app.ApplicantId,
                        Action = ReviewActionConstants.PaymentTimeout,
                        OldStatus = oldStatus,
                        NewStatus = ApplicationStatusConstants.Expired,
                        Note = $"Tự động hủy do quá hạn thanh toán đặt cọc ({depositHours} giờ — PolicyConfig DEPOSIT_PAYMENT_HOURS).",
                        ChangedAt = DateTime.UtcNow
                    };
                    context.ApplicationStatusHistories.Add(history);

                    await context.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);

                    try
                    {
                        await notificationService.SendAsync(
                            app.ApplicantId,
                            "Hồ sơ đã hết hạn thanh toán",
                            $"Hồ sơ của bạn đã bị hủy do không thanh toán đặt cọc trong vòng {depositHours} giờ.",
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
    }
}
