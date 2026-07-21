using RHS.Application.Interfaces;

namespace RHS.API.BackgroundServices;

/// <summary>
/// Background service chạy mỗi đêm (00:30 UTC).
/// Scan PaymentInstallments quá hạn (DueDate &lt; UtcNow AND Status == PENDING)
///   → đánh dấu OVERDUE + gửi notification nhắc nhở.
/// KHÔNG tự động hủy hợp đồng.
/// </summary>
public class OverduePaymentWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OverduePaymentWorker> _logger;

    public OverduePaymentWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OverduePaymentWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OverduePaymentWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Tính thời gian chờ tới 00:30 UTC ngày tiếp theo
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1).AddMinutes(30); // 00:30 UTC tomorrow
            var delay = nextRun - now;

            _logger.LogInformation(
                "OverduePaymentWorker next run at {NextRun:yyyy-MM-dd HH:mm} UTC (in {Hours}h {Minutes}m).",
                nextRun, (int)delay.TotalHours, delay.Minutes);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            _logger.LogInformation("OverduePaymentWorker executing overdue scan...");

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var installmentService = scope.ServiceProvider
                    .GetRequiredService<IInstallmentService>();

                await installmentService.ProcessOverdueInstallmentsAsync(stoppingToken);

                _logger.LogInformation("OverduePaymentWorker completed scan successfully.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in OverduePaymentWorker during overdue scan.");
            }
        }

        _logger.LogInformation("OverduePaymentWorker stopped.");
    }
}
