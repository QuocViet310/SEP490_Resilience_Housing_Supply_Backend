using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Application.Interfaces;
using RHS.Infrastructure.Data;
using RHS.Infrastructure.Helpers;

namespace RHS.API.BackgroundServices;

/// <summary>
/// Hết hạn theo luồng chuẩn:
/// - CONTRACT_PENDING quá hạn ký HĐ → EXPIRED (+ hoàn 1 suất căn)
/// - CONTRACT_SIGNED quá hạn đặt cọc (từ SignedAt) → EXPIRED (+ hoàn 1 suất căn)
/// - Người được đôn từ Danh sách dự bị quá hạn xác nhận → mất suất, gọi người kế tiếp
/// Không expire APPROVED: sau duyệt còn chờ CĐT chốt / bốc thăm, chưa giữ suất.
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

        var depositHours = await policyService.GetValueAsync(PolicyKeys.DepositPaymentHours, 168, stoppingToken);
        var contractDays = await policyService.GetValueAsync(PolicyKeys.ContractSigningDeadlineDays, 15, stoppingToken);

        var depositCutoff = DateTime.UtcNow.AddHours(-depositHours);
        var contractCutoff = DateTime.UtcNow.AddDays(-contractDays);

        // Quá hạn ký HĐ nguyên tắc — mốc PrincipleAgreement.CreatedAt (khi chốt suất), không dùng UpdatedAt
        var pendingSignExpired = await context.HousingApplications
            .Include(x => x.PrincipleAgreement)
            .Where(x =>
                x.ApplicationStatus == ApplicationStatusConstants.ContractPending &&
                (
                    (x.PrincipleAgreement != null && x.PrincipleAgreement.CreatedAt < contractCutoff)
                    || (x.PrincipleAgreement == null && (x.UpdatedAt ?? x.SubmittedAt) < contractCutoff)
                ))
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

        await ProcessExpiredWaitlistPromotionsAsync(scope, context, notificationService, stoppingToken);

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
                note: $"Tự động hủy do quá hạn thanh toán Đợt 1 sau khi ký HĐ ({depositHours} giờ — PolicyConfig DEPOSIT_PAYMENT_HOURS).",
                notifTitle: "Hồ sơ đã hết hạn thanh toán",
                notifBody: $"Hồ sơ của bạn đã bị hủy do không thanh toán Đợt 1 trong vòng {depositHours} giờ sau khi ký hợp đồng mua bán nhà ở xã hội.",
                stoppingToken);
        }
    }

    /// <summary>
    /// Người được đôn từ Danh sách dự bị có hạn xác nhận cụ thể (PolicyConfig WAITLIST_CONFIRM_HOURS).
    /// Quá hạn mà chưa nộp tiền thì mất suất, căn được chuyển ngay cho người kế tiếp trong danh sách —
    /// đây là điều làm cho hạn xác nhận có hiệu lực thật thay vì chỉ là một mốc hiển thị.
    /// </summary>
    private async Task ProcessExpiredWaitlistPromotionsAsync(
        IServiceScope scope,
        AppDbContext context,
        INotificationService notificationService,
        CancellationToken stoppingToken)
    {
        var now = DateTime.UtcNow;

        var promotedStatuses = new[]
        {
            ApplicationStatusConstants.LotteryWon,
            ApplicationStatusConstants.DepositPending
        };

        var expiredPromotions = await context.HousingApplications
            .Where(x => promotedStatuses.Contains(x.ApplicationStatus)
                        && x.WaitlistPromotedAt.HasValue
                        && x.DepositDeadline.HasValue
                        && x.DepositDeadline.Value < now)
            .ToListAsync(stoppingToken);

        if (expiredPromotions.Count == 0)
            return;

        var lotteryService = scope.ServiceProvider.GetRequiredService<ILotteryService>();

        foreach (var app in expiredPromotions)
        {
            var isPaid = await context.Payments.AnyAsync(
                p => p.ApplicationId == app.ApplicationId && p.Status == "Success",
                stoppingToken);

            if (isPaid)
                continue;

            var projectId = app.ProjectId;
            var releasedApartmentId = app.ApartmentId;
            var forfeitedRank = app.WaitlistNumber;
            var desiredTypeId = app.DesiredApartmentTypeId;
            var deadline = app.DepositDeadline!.Value;
            var oldStatus = app.ApplicationStatus;

            app.ApplicationStatus = ApplicationStatusConstants.LotteryLost;
            app.LotteryResult = LotteryResultConstants.Lost;
            app.ApartmentId = null;
            // Đã dùng hết lượt được gọi — không xếp lại vào danh sách dự bị.
            app.WaitlistNumber = null;
            app.UpdatedAt = now;

            if (releasedApartmentId.HasValue && releasedApartmentId.Value != Guid.Empty)
            {
                var apartment = await context.Apartments
                    .FirstOrDefaultAsync(a => a.Id == releasedApartmentId.Value, stoppingToken);
                if (apartment != null)
                {
                    apartment.Status = ApartmentStatusConstants.Available;
                    apartment.UpdatedAt = now;
                }
            }

            context.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                HistoryId = Guid.NewGuid(),
                ApplicationId = app.ApplicationId,
                ChangedBy = app.ApplicantId,
                Action = ReviewActionConstants.WaitlistForfeited,
                OldStatus = oldStatus,
                NewStatus = ApplicationStatusConstants.LotteryLost,
                Note = $"Được đôn từ Danh sách dự bị #{forfeitedRank} nhưng không xác nhận nộp tiền " +
                       $"trước hạn {deadline:dd/MM/yyyy HH:mm} nên mất suất. " +
                       "Căn hộ được chuyển cho người kế tiếp trong Danh sách dự bị.",
                ChangedAt = now
            });

            await context.SaveChangesAsync(stoppingToken);

            _logger.LogInformation(
                "Waitlist promotion forfeited: App={AppId}, Rank={Rank}, Deadline={Deadline}.",
                app.ApplicationId, forfeitedRank, deadline);

            try
            {
                await notificationService.SendAsync(
                    app.ApplicantId,
                    "Bạn đã mất suất mua do quá hạn xác nhận",
                    $"Hồ sơ của bạn được chuyển quyền mua từ Danh sách dự bị nhưng chưa nộp tiền đợt 1 " +
                    $"trước hạn {deadline:dd/MM/yyyy HH:mm}, nên suất đã được chuyển cho người kế tiếp.",
                    NotificationTypeConstants.ApplicationExpired);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send waitlist forfeiture notification for App={AppId}.", app.ApplicationId);
            }

            // Đôn người kế tiếp — cùng đường đôn duy nhất, tự đồng bộ lại số suất khả dụng.
            var promoted = await lotteryService.PromoteNextWaitlistApplicantAsync(
                projectId, desiredTypeId, releasedApartmentId, stoppingToken);

            if (promoted == null)
            {
                await ProjectUnitSeatHelper.SyncAvailableUnitsAsync(context, projectId, _logger, stoppingToken);
                await context.SaveChangesAsync(stoppingToken);
            }
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
            var apartmentId = app.ApartmentId;
            app.ApplicationStatus = ApplicationStatusConstants.Expired;
            app.ApartmentId = null;
            app.UpdatedAt = DateTime.UtcNow;
            context.HousingApplications.Update(app);

            var released = await ProjectUnitSeatHelper.TryReleaseReservedUnitAsync(
                context, app.ProjectId, oldStatus, apartmentId, _logger, stoppingToken);

            var historyNote = released
                ? $"{note} Đã hoàn căn về dự án (AVAILABLE)."
                : note;

            context.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                HistoryId = Guid.NewGuid(),
                ApplicationId = app.ApplicationId,
                ChangedBy = app.ApplicantId,
                Action = action,
                OldStatus = oldStatus,
                NewStatus = ApplicationStatusConstants.Expired,
                Note = historyNote,
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
