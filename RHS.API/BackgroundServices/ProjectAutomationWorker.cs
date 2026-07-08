using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Application.Interfaces;
using RHS.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RHS.API.BackgroundServices;

/// <summary>
/// Worker chạy định kỳ hàng ngày để tự động hóa vòng đời dự án (UPCOMING -> OPEN -> CLOSED)
/// và tự động duyệt hồ sơ sau 20 ngày chờ duyệt từ SXD (Tacit Approval).
/// </summary>
public class ProjectAutomationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectAutomationWorker> _logger;
    private readonly TimeSpan _period = TimeSpan.FromHours(12); // Quét mỗi 12 tiếng để đảm bảo không bị lỡ do server khởi động lại

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
        
        // Chạy lần đầu ngay khi start
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

        var now = DateTime.UtcNow;

        _logger.LogInformation("ProjectAutomationWorker executing tasks at {Time}", now);

        // ───────────────────────────────────────────────────────────────────
        // NHIỆM VỤ 1: VÒNG ĐỜI DỰ ÁN (UPCOMING -> OPEN -> CLOSED)
        // ───────────────────────────────────────────────────────────────────
        
        // 1.1 Lấy các status từ DB
        var openStatus = await context.HousingProjectStatuses.FirstOrDefaultAsync(s => s.StatusCode == "OPEN", stoppingToken);
        var closedStatus = await context.HousingProjectStatuses.FirstOrDefaultAsync(s => s.StatusCode == "CLOSED", stoppingToken);

        if (openStatus != null)
        {
            // UPCOMING -> OPEN
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
                _logger.LogInformation("Worker: Mở nhận hồ sơ dự án {ProjectName} ({Id})", proj.ProjectName, proj.Id);
                proj.HousingProjectStatusId = openStatus.Id;
                proj.HousingProjectStatus = openStatus;
                proj.UpdatedAt = now;
            }
        }

        if (closedStatus != null)
        {
            // OPEN -> CLOSED
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

        // ───────────────────────────────────────────────────────────────────
        // NHIỆM VỤ 2: QUY TẮC 20 NGÀY TỰ ĐỘNG PHÊ DUYỆT (PENDING_SXD_REVIEW -> APPROVED)
        // ───────────────────────────────────────────────────────────────────
        var cutoff = now.AddDays(-20);

        var pendingApps = await context.HousingApplications
            .Include(a => a.HousingProject)
            .Include(a => a.StatusHistories)
            .Where(a => a.ApplicationStatus == ApplicationStatusConstants.PendingSxdReview)
            .ToListAsync(stoppingToken);

        foreach (var app in pendingApps)
        {
            // Tìm ngày chuyển sang trạng thái PENDING_SXD_REVIEW gần nhất
            var sxdSubmissionDate = app.StatusHistories
                .Where(h => h.NewStatus == ApplicationStatusConstants.PendingSxdReview)
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => (DateTime?)h.ChangedAt)
                .FirstOrDefault() ?? app.UpdatedAt ?? app.CreatedAt;

            if (sxdSubmissionDate <= cutoff)
            {
                _logger.LogInformation("Worker: Tự động phê duyệt hồ sơ {AppId} (quá 20 ngày chờ duyệt SXD)", app.ApplicationId);

                using var transaction = await context.Database.BeginTransactionAsync(stoppingToken);
                try
                {
                    var project = app.HousingProject;
                    if (project == null)
                    {
                        _logger.LogWarning("Worker: Không tìm thấy dự án cho hồ sơ {AppId}", app.ApplicationId);
                        continue;
                    }

                    if (project.AvailableUnits <= 0)
                    {
                        _logger.LogWarning("Worker: Dự án {ProjectName} đã hết căn hộ trống. Không thể auto approve hồ sơ {AppId}", project.ProjectName, app.ApplicationId);
                        continue;
                    }

                    // Giảm căn hộ trống
                    project.AvailableUnits -= 1;
                    project.UpdatedAt = now;
                    context.HousingProjects.Update(project);

                    // Phê duyệt hồ sơ
                    var oldStatus = app.ApplicationStatus;
                    app.ApplicationStatus = ApplicationStatusConstants.Approved;
                    app.UpdatedAt = now;
                    app.FinalDecisionDate = now;

                    // Lưu lịch sử duyệt
                    var history = new ApplicationStatusHistory
                    {
                        HistoryId = Guid.NewGuid(),
                        ApplicationId = app.ApplicationId,
                        ChangedBy = RoleConstants.SystemAdministratorId, // Đại diện bởi System Admin
                        Action = ReviewActionConstants.Approve,
                        OldStatus = oldStatus,
                        NewStatus = ApplicationStatusConstants.Approved,
                        Note = "Tự động phê duyệt theo quy định 20 ngày",
                        ChangedAt = now
                    };
                    context.ApplicationStatusHistories.Add(history);

                    await context.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);

                    // Gửi thông báo cho người nộp đơn
                    await notificationService.SendAsync(
                        app.ApplicantId,
                        "Hồ sơ được tự động phê duyệt",
                        "Hồ sơ của bạn đã được tự động phê duyệt do quá 20 ngày thẩm định theo quy định của Sở Xây Dựng. Vui lòng thanh toán đặt cọc để giữ chỗ.",
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
