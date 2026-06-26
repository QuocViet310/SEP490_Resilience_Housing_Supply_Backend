using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RHS.API.BackgroundServices;

public class PaymentTimeoutWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentTimeoutWorker> _logger;
    private readonly TimeSpan _period = TimeSpan.FromMinutes(10); // Check every 10 minutes

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

        var cutoffTime = DateTime.UtcNow.AddHours(-24); // 24-hour payment deadline

        // Get all APPROVED applications that were approved before cutoffTime
        // Skip DEPOSIT_PAID (already paid successfully)
        var expiredApplications = await context.HousingApplications
            .Where(x => x.ApplicationStatus == ApplicationStatusConstants.Approved 
                     && x.FinalDecisionDate.HasValue 
                     && x.FinalDecisionDate.Value < cutoffTime)
            .ToListAsync(stoppingToken);

        if (!expiredApplications.Any())
        {
            return;
        }

        _logger.LogInformation("Found {Count} potentially expired approved applications.", expiredApplications.Count);

        foreach (var app in expiredApplications)
        {
            // Check if there is any successful payment linked directly to this application
            var isPaid = await context.Payments.AnyAsync(p =>
                p.ApplicationId == app.ApplicationId &&
                p.Status == "Success",
                stoppingToken);

            if (!isPaid)
            {
                _logger.LogInformation("Application {AppId} unpaid after 24h. Expiring and releasing unit.", app.ApplicationId);

                using var transaction = await context.Database.BeginTransactionAsync(stoppingToken);
                try
                {
                    // Reload project tracking to avoid tracking issues
                    var project = await context.HousingProjects.FirstOrDefaultAsync(p => p.Id == app.ProjectId, stoppingToken);
                    if (project != null)
                    {
                        project.AvailableUnits += 1;
                        project.UpdatedAt = DateTime.UtcNow;
                        context.HousingProjects.Update(project);
                    }

                    // Update application status to EXPIRED
                    var oldStatus = app.ApplicationStatus;
                    app.ApplicationStatus = ApplicationStatusConstants.Expired;
                    app.UpdatedAt = DateTime.UtcNow;
                    context.HousingApplications.Update(app);

                    // Append status history
                    var history = new ApplicationStatusHistory
                    {
                        HistoryId = Guid.NewGuid(),
                        ApplicationId = app.ApplicationId,
                        ChangedBy = app.ApplicantId, // Fallback to applicant ID (or system user if one existed)
                        Action = ReviewActionConstants.PaymentTimeout,
                        OldStatus = oldStatus,
                        NewStatus = ApplicationStatusConstants.Expired,
                        Note = "Tự động hủy do quá hạn thanh toán đặt cọc (24 giờ).",
                        ChangedAt = DateTime.UtcNow
                    };
                    context.ApplicationStatusHistories.Add(history);

                    await context.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);
                    
                    _logger.LogInformation("Successfully expired application {AppId} and released 1 unit for project {ProjectId}.", app.ApplicationId, app.ProjectId);
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
