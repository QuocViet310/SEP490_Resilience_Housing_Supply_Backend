using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.Lottery;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Services;

public class LotteryService : ILotteryService
{
    private readonly AppDbContext _db;
    private readonly ILogger<LotteryService> _logger;

    public LotteryService(AppDbContext db, ILogger<LotteryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<LotteryDrawResultDto> RunLotteryAsync(
        Guid projectId,
        Guid drawnBy,
        int? totalUnits = null,
        CancellationToken ct = default)
    {
        var project = await _db.HousingProjects
            .Include(p => p.HousingQuotas)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy dự án.");

        var qualifiedStatuses = new[]
        {
            ApplicationStatusConstants.Approved,
            ApplicationStatusConstants.ApprovedByTimeout,
            ApplicationStatusConstants.DepositPaid
        };

        var participants = await _db.HousingApplications
            .Include(a => a.PrincipleAgreement)
            .Where(a => a.ProjectId == projectId
                        && qualifiedStatuses.Contains(a.ApplicationStatus)
                        && !a.IsViolation)
            .OrderBy(a => a.SubmittedAt)
            .ToListAsync(ct);

        if (participants.Count == 0)
            throw new InvalidOperationException("Không có hồ sơ đủ điều kiện để bốc thăm.");

        // Hướng A: nếu đã bốc thăm trước đó, hoàn suất của người từng trúng trước khi phân bổ lại.
        var wonResults = new[]
        {
            LotteryResultConstants.Won,
            LotteryResultConstants.PriorityWon
        };
        var previousWinners = participants.Count(a =>
            a.LotteryResult != null && wonResults.Contains(a.LotteryResult));
        if (previousWinners > 0)
        {
            project.AvailableUnits += previousWinners;
            _logger.LogInformation(
                "Lottery re-run for project {ProjectId}: restored {Count} units before redraw. AvailableUnits={Units}.",
                projectId, previousWinners, project.AvailableUnits);
        }

        // Trần phân bổ = AvailableUnits (số căn công bố / còn lại), không mặc định = số người nộp.
        if (project.AvailableUnits <= 0)
            throw new InvalidOperationException(
                "Dự án đã hết suất để phân bổ qua bốc thăm (AvailableUnits = 0).");

        var units = totalUnits ?? project.AvailableUnits;
        if (units <= 0)
            throw new ArgumentException("TotalUnits phải lớn hơn 0.");
        if (units > project.AvailableUnits)
            units = project.AvailableUnits;
        if (units > participants.Count)
            units = participants.Count;

        var priorityApps = participants
            .Where(a => !string.IsNullOrWhiteSpace(a.PriorityGroup))
            .OrderBy(a => a.SubmittedAt)
            .ToList();

        var nonPriority = participants
            .Where(a => string.IsNullOrWhiteSpace(a.PriorityGroup))
            .ToList();

        // Đ38.2: số căn ưu tiên = (số HS ưu tiên / tổng HS) * tổng căn
        var priorityQuota = (int)Math.Floor(
            (double)priorityApps.Count / participants.Count * units);
        if (priorityApps.Count > 0 && priorityQuota == 0 && units > 0)
            priorityQuota = 1;
        if (priorityQuota > priorityApps.Count)
            priorityQuota = priorityApps.Count;
        if (priorityQuota > units)
            priorityQuota = units;

        var winners = new HashSet<Guid>();
        var results = new List<LotteryParticipantResultDto>();
        var now = DateTime.UtcNow;

        // Ưu tiên không bốc thăm — xếp theo SubmittedAt
        var priorityWinners = priorityApps.Take(priorityQuota).ToList();
        foreach (var app in priorityWinners)
        {
            winners.Add(app.ApplicationId);
            app.LotteryResult = LotteryResultConstants.PriorityWon;
            app.ApplicationStatus = ApplicationStatusConstants.ContractPending;
            app.UpdatedAt = now;
            results.Add(MapParticipant(app, LotteryResultConstants.PriorityWon, true));

            if (app.PrincipleAgreement == null)
            {
                _db.PrincipleAgreements.Add(new PrincipleAgreement
                {
                    Id = Guid.NewGuid(),
                    ApplicationId = app.ApplicationId,
                    PdfUrl = $"/api/payment/download-contract/{app.ApplicationId}",
                    CreatedAt = now
                });
            }

            _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                HistoryId = Guid.NewGuid(),
                ApplicationId = app.ApplicationId,
                ChangedBy = drawnBy,
                Action = ReviewActionConstants.PriorityDirectApproval,
                OldStatus = app.ApplicationStatus,
                NewStatus = ApplicationStatusConstants.ContractPending,
                Note = "Hồ sơ thuộc diện ưu tiên được phê duyệt trực tiếp.",
                ChangedAt = now
            });
        }

        foreach (var app in priorityApps.Skip(priorityQuota))
        {
            // Ưu tiên dư → tham gia pool random cùng non-priority
            nonPriority.Add(app);
        }

        var remainingUnits = units - priorityWinners.Count;
        var seed = Environment.TickCount;
        var rng = new Random(seed);

        var shuffled = nonPriority.OrderBy(_ => rng.Next()).ToList();
        var randomWinners = shuffled.Take(remainingUnits).ToList();
        var randomLosers = shuffled.Skip(remainingUnits).ToList();

        foreach (var app in randomWinners)
        {
            winners.Add(app.ApplicationId);
            app.LotteryResult = LotteryResultConstants.Won;
            app.ApplicationStatus = ApplicationStatusConstants.ContractPending;
            app.UpdatedAt = now;
            results.Add(MapParticipant(app, LotteryResultConstants.Won, !string.IsNullOrWhiteSpace(app.PriorityGroup)));

            if (app.PrincipleAgreement == null)
            {
                _db.PrincipleAgreements.Add(new PrincipleAgreement
                {
                    Id = Guid.NewGuid(),
                    ApplicationId = app.ApplicationId,
                    PdfUrl = $"/api/payment/download-contract/{app.ApplicationId}",
                    CreatedAt = now
                });
            }

            _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                HistoryId = Guid.NewGuid(),
                ApplicationId = app.ApplicationId,
                ChangedBy = drawnBy,
                Action = ReviewActionConstants.LotteryWon,
                OldStatus = app.ApplicationStatus,
                NewStatus = ApplicationStatusConstants.ContractPending,
                Note = "Hồ sơ trúng bốc thăm, chuyển sang bước ký hợp đồng nguyên tắc.",
                ChangedAt = now
            });
        }

        foreach (var app in randomLosers)
        {
            app.LotteryResult = LotteryResultConstants.Lost;
            app.ApplicationStatus = ApplicationStatusConstants.LotteryLost;
            app.UpdatedAt = now;
            results.Add(MapParticipant(app, LotteryResultConstants.Lost, !string.IsNullOrWhiteSpace(app.PriorityGroup)));

            _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                HistoryId = Guid.NewGuid(),
                ApplicationId = app.ApplicationId,
                ChangedBy = drawnBy,
                Action = ReviewActionConstants.LotteryLost,
                OldStatus = app.ApplicationStatus,
                NewStatus = ApplicationStatusConstants.LotteryLost,
                Note = "Hồ sơ trượt bốc thăm.",
                ChangedAt = now
            });
        }

        // Cập nhật HousingQuota RemainingSlots
        foreach (var quota in project.HousingQuotas)
        {
            var used = priorityWinners.Count(a =>
                string.Equals(a.PriorityGroup, quota.PriorityGroup, StringComparison.OrdinalIgnoreCase));
            quota.RemainingSlots = Math.Max(0, quota.AllocatedSlots - used);
        }

        // Trừ suất khi chính thức trúng (WON / PRIORITY_WON)
        var winnerCount = priorityWinners.Count + randomWinners.Count;
        project.AvailableUnits -= winnerCount;
        if (project.AvailableUnits < 0)
            project.AvailableUnits = 0;
        project.UpdatedAt = DateTime.UtcNow;

        var draw = new LotteryDraw
        {
            DrawId = Guid.NewGuid(),
            ProjectId = projectId,
            DrawnBy = drawnBy,
            DrawnAt = DateTime.UtcNow,
            TotalUnits = units,
            PriorityAllocated = priorityWinners.Count,
            RandomAllocated = randomWinners.Count,
            TotalParticipants = participants.Count,
            RandomSeed = seed,
            ResultJson = JsonSerializer.Serialize(results.Select(r => new
            {
                r.ApplicationId,
                r.Result,
                r.SlotCode,
                r.IsPriority
            }))
        };

        _db.LotteryDraws.Add(draw);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Lottery draw {DrawId} for project {ProjectId}: {Priority} priority + {Random} random / {Units} units, {Participants} participants. Remaining AvailableUnits={Remaining}.",
            draw.DrawId, projectId, draw.PriorityAllocated, draw.RandomAllocated, units, participants.Count, project.AvailableUnits);

        var drawer = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == drawnBy, ct);

        return new LotteryDrawResultDto
        {
            DrawId = draw.DrawId,
            ProjectId = projectId,
            DrawnAt = draw.DrawnAt,
            DrawnBy = drawnBy,
            DrawnByName = drawer?.FullName,
            TotalUnits = draw.TotalUnits,
            PriorityAllocated = draw.PriorityAllocated,
            RandomAllocated = draw.RandomAllocated,
            TotalParticipants = draw.TotalParticipants,
            RandomSeed = seed,
            Participants = results.OrderBy(r => r.Result).ThenBy(r => r.FullName).ToList()
        };
    }

    public async Task<LotteryDrawResultDto?> GetLatestResultAsync(Guid projectId, CancellationToken ct = default)
    {
        var draw = await _db.LotteryDraws
            .AsNoTracking()
            .Include(d => d.DrawnByUser)
            .Where(d => d.ProjectId == projectId)
            .OrderByDescending(d => d.DrawnAt)
            .FirstOrDefaultAsync(ct);

        if (draw is null) return null;

        var apps = await _db.HousingApplications
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId
                        && a.ApplicationStatus == ApplicationStatusConstants.DepositPaid
                        && a.LotteryResult != null)
            .ToListAsync(ct);

        return new LotteryDrawResultDto
        {
            DrawId = draw.DrawId,
            ProjectId = draw.ProjectId,
            DrawnAt = draw.DrawnAt,
            DrawnBy = draw.DrawnBy,
            DrawnByName = draw.DrawnByUser?.FullName,
            TotalUnits = draw.TotalUnits,
            PriorityAllocated = draw.PriorityAllocated,
            RandomAllocated = draw.RandomAllocated,
            TotalParticipants = draw.TotalParticipants,
            RandomSeed = draw.RandomSeed,
            Participants = apps.Select(a => MapParticipant(
                    a,
                    a.LotteryResult ?? LotteryResultConstants.Pending,
                    !string.IsNullOrWhiteSpace(a.PriorityGroup)))
                .OrderBy(r => r.Result)
                .ThenBy(r => r.FullName)
                .ToList()
        };
    }

    private static LotteryParticipantResultDto MapParticipant(
        HousingApplication app,
        string result,
        bool isPriority) => new()
    {
        ApplicationId = app.ApplicationId,
        FullName = app.FullName,
        CitizenId = app.CitizenId,
        SlotCode = app.SlotCode,
        PriorityGroup = app.PriorityGroup,
        Result = result,
        IsPriority = isPriority
    };
}
