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
/// Worker chạy định kỳ để tự động hóa vòng đời dự án và tacit approval (số ngày từ PolicyConfig).
/// </summary>
public class ProjectAutomationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectAutomationWorker> _logger;
    private readonly TimeSpan _period = TimeSpan.FromHours(12);

    public ProjectAutomationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectAutomationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProjectAutomationWorker started.");

        using var timer = new PeriodicTimer(_period);

        try
        {
            await ProcessAutomationAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred in initial run of ProjectAutomationWorker.");
        }

        while (await timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAutomationAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in ProjectAutomationWorker loop.");
            }
        }
    }

    private async Task ProcessAutomationAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var policyService = scope.ServiceProvider.GetRequiredService<IPolicyService>();

        var now = DateTime.UtcNow;
        var announceMinDays = await policyService.GetValueAsync(PolicyKeys.PublicAnnounceMinDays, 30, stoppingToken);
        var tacitDays = await policyService.GetValueAsync(PolicyKeys.TacitApprovalDays, 20, stoppingToken);

        _logger.LogInformation(
            "ProjectAutomationWorker executing at {Time}. AnnounceMinDays={Announce}, TacitDays={Tacit}",
            now, announceMinDays, tacitDays);

        var openStatus = await context.HousingProjectStatuses.FirstOrDefaultAsync(s => s.StatusCode == "OPEN", stoppingToken);
        var closedStatus = await context.HousingProjectStatuses.FirstOrDefaultAsync(s => s.StatusCode == "CLOSED", stoppingToken);

        if (openStatus != null)
        {
            var upcomingProjects = await context.HousingProjects
                .Include(p => p.HousingProjectStatus)
                .Where(p => p.HousingProjectStatus != null
                         && p.HousingProjectStatus.StatusCode == "UPCOMING"
                         && p.ApplicationOpenDate.HasValue
                         && p.ApplicationOpenDate.Value <= now
                         && !p.IsDeleted)
                .ToListAsync(stoppingToken);

            foreach (var proj in upcomingProjects)
            {
                // Đ38.1.b — block OPEN nếu chưa công bố đủ số ngày
                var announceAt = proj.PublicAnnounceAt ?? proj.CreatedAt;
                if (announceAt.AddDays(announceMinDays) > now)
                {
                    _logger.LogInformation(
                        "Worker: Dự án {ProjectName} chưa đủ {Days} ngày công bố (từ {AnnounceAt}). Giữ UPCOMING.",
                        proj.ProjectName, announceMinDays, announceAt);
                    continue;
                }

                _logger.LogInformation("Worker: Mở nhận hồ sơ dự án {ProjectName} ({Id})", proj.ProjectName, proj.Id);
                proj.HousingProjectStatusId = openStatus.Id;
                proj.HousingProjectStatus = openStatus;
                proj.UpdatedAt = now;
            }
        }

        if (closedStatus != null)
        {
            var openProjects = await context.HousingProjects
                .Include(p => p.HousingProjectStatus)
                .Where(p => p.HousingProjectStatus != null
                         && p.HousingProjectStatus.StatusCode == "OPEN"
                         && p.ApplicationCloseDate.HasValue
                         && p.ApplicationCloseDate.Value < now
                         && !p.IsDeleted)
                .ToListAsync(stoppingToken);

            foreach (var proj in openProjects)
            {
                _logger.LogInformation("Worker: Đóng nhận hồ sơ dự án {ProjectName} ({Id})", proj.ProjectName, proj.Id);
                proj.HousingProjectStatusId = closedStatus.Id;
                proj.HousingProjectStatus = closedStatus;
                proj.UpdatedAt = now;
            }
        }

        await context.SaveChangesAsync(stoppingToken);

        var cutoff = now.AddDays(-tacitDays);

        var pendingApps = await context.HousingApplications
            .Include(a => a.HousingProject)
            .Include(a => a.StatusHistories)
            .Where(a => a.ApplicationStatus == ApplicationStatusConstants.PendingSxdReview)
            .ToListAsync(stoppingToken);

        foreach (var app in pendingApps)
        {
            var sxdSubmissionDate = app.StatusHistories
                .Where(h => h.NewStatus == ApplicationStatusConstants.PendingSxdReview)
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => (DateTime?)h.ChangedAt)
                .FirstOrDefault() ?? app.UpdatedAt ?? app.CreatedAt;

            if (sxdSubmissionDate <= cutoff)
            {
                _logger.LogInformation(
                    "Worker: Tự động phê duyệt hồ sơ {AppId} (quá {Days} ngày chờ duyệt SXD)",
                    app.ApplicationId, tacitDays);

                using var transaction = await context.Database.BeginTransactionAsync(stoppingToken);
                try
                {
                    var project = app.HousingProject;
                    if (project == null)
                    {
                        _logger.LogWarning("Worker: Không tìm thấy dự án cho hồ sơ {AppId}", app.ApplicationId);
                        continue;
                    }

                    // Hướng A: không chặn/trừ AvailableUnits khi duyệt — suất phân bổ lúc bốc thăm.
                    // Cross-check Đ38.1.đ trước tacit approval
                    var blockedStatuses = new[]
                    {
                        ApplicationStatusConstants.Approved,
                        ApplicationStatusConstants.DepositPaid
                    };
                    var alreadySupported = await context.HousingApplications.AnyAsync(a =>
                        a.ApplicationId != app.ApplicationId
                        && a.CitizenId == app.CitizenId
                        && blockedStatuses.Contains(a.ApplicationStatus), stoppingToken);

                    if (alreadySupported)
                    {
                        _logger.LogWarning(
                            "Worker: Bỏ qua tacit approval {AppId} — CCCD đã được hỗ trợ ở hồ sơ khác.",
                            app.ApplicationId);
                        continue;
                    }

                    var oldStatus = app.ApplicationStatus;
                    app.ApplicationStatus = ApplicationStatusConstants.Approved;
                    app.UpdatedAt = now;
                    app.FinalDecisionDate = now;

                    var history = new ApplicationStatusHistory
                    {
                        HistoryId = Guid.NewGuid(),
                        ApplicationId = app.ApplicationId,
                        ChangedBy = RoleConstants.SystemAdministratorId,
                        Action = ReviewActionConstants.Approve,
                        OldStatus = oldStatus,
                        NewStatus = ApplicationStatusConstants.Approved,
                        Note = $"Tự động phê duyệt theo quy định {tacitDays} ngày (PolicyConfig TACIT_APPROVAL_DAYS)",
                        ChangedAt = now
                    };
                    context.ApplicationStatusHistories.Add(history);

                    await context.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);

                    await notificationService.SendAsync(
                        app.ApplicantId,
                        "Hồ sơ được tự động phê duyệt",
                        $"Hồ sơ của bạn đã được tự động phê duyệt do quá {tacitDays} ngày thẩm định theo quy định của Sở Xây Dựng. Vui lòng thanh toán đặt cọc để đủ điều kiện tham gia bốc thăm.",
                        NotificationTypeConstants.ApplicationApproved
                    );
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(stoppingToken);
                    _logger.LogError(ex, "Worker: Lỗi xảy ra khi tự động phê duyệt hồ sơ {AppId}", app.ApplicationId);
                }
            }
        }
    }
}
