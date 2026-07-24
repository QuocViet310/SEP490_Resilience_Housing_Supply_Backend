using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.Lottery;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;
using RHS.Infrastructure.Hubs;

namespace RHS.Infrastructure.Services;

public class LotteryService : ILotteryService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly IHubContext<LotteryHub, ILotteryHubClient> _hubContext;
    private readonly ILogger<LotteryService> _logger;

    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> ProjectLocks = new();

    /// <summary>Pool eligible: hồ sơ Sở đã duyệt.</summary>
    private static readonly string[] BatchEligibleStatuses = new[]
    {
        ApplicationStatusConstants.Approved,
        ApplicationStatusConstants.ApprovedByTimeout
    };

    public LotteryService(
        AppDbContext db,
        INotificationService notificationService,
        IHubContext<LotteryHub, ILotteryHubClient> hubContext,
        ILogger<LotteryService> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<LotteryScheduleDetailDto> ScheduleLotteryAsync(
        Guid projectId,
        CreateOrUpdateLotteryScheduleDto dto,
        Guid createdBy,
        CancellationToken ct = default)
    {
        var project = await _db.HousingProjects
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct)
            ?? throw new InvalidOperationException("Không tìm thấy dự án.");

        project.LotteryDate = dto.LotteryDate;
        project.LotteryLocation = dto.LotteryLocation;
        project.LotteryType = dto.LotteryType;
        project.LotteryDescription = dto.LotteryDescription;
        project.IsLotteryApproved = false; // Chờ Admin/Sở duyệt
        project.LotterySessionStatus = LotterySessionStatusConstants.Scheduled;
        project.LotteryJoinCode = null;
        project.UpdatedAt = DateTime.UtcNow;

        if (dto.TotalUnits.HasValue && dto.TotalUnits.Value > 0)
        {
            project.AvailableUnits = dto.TotalUnits.Value;
        }

        await _db.SaveChangesAsync(ct);

        await BroadcastStatusAsync(projectId, project.LotterySessionStatus);
        return await BuildLotteryScheduleDetailDtoAsync(project, ct);
    }

    public async Task<LotteryScheduleDetailDto> ApproveLotteryScheduleAsync(
        Guid projectId,
        Guid approvedBy,
        CancellationToken ct = default)
    {
        var project = await _db.HousingProjects
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct)
            ?? throw new InvalidOperationException("Không tìm thấy dự án.");

        if (!project.LotteryDate.HasValue)
            throw new InvalidOperationException("Dự án chưa có lịch bốc thăm để duyệt.");

        project.IsLotteryApproved = true;
        project.LotteryApprovedAt = DateTime.UtcNow;
        project.LotteryApprovedBy = approvedBy;
        project.LotterySessionStatus = LotterySessionStatusConstants.Scheduled;
        project.LotteryJoinCode = GenerateJoinCode();
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Gửi thông báo đến toàn bộ ứng viên đủ điều kiện thuộc dự án
        var eligibleApplicants = await _db.HousingApplications
            .Where(a => a.ProjectId == projectId
                        && BatchEligibleStatuses.Contains(a.ApplicationStatus)
                        && !a.IsViolation)
            .Select(a => a.ApplicantId)
            .Distinct()
            .ToListAsync(ct);

        var notifTitle = "Lịch bốc thăm đã được phê duyệt & công bố";
        var notifContent =
            $"Dự án '{project.ProjectName}' đã chốt lịch bốc thăm vào lúc {project.LotteryDate:dd/MM/yyyy HH:mm} tại {project.LotteryLocation}. " +
            $"Hình thức: {project.LotteryType}. Mã OTP vào sảnh: {project.LotteryJoinCode}.";

        foreach (var applicantId in eligibleApplicants)
        {
            try
            {
                await _notificationService.SendAsync(
                    applicantId,
                    notifTitle,
                    notifContent,
                    NotificationTypeConstants.LotteryScheduled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi gửi thông báo lịch bốc thăm cho user {UserId}", applicantId);
            }
        }

        await BroadcastStatusAsync(projectId, project.LotterySessionStatus!);
        return await BuildLotteryScheduleDetailDtoAsync(project, ct);
    }

    public async Task<LotteryScheduleDetailDto?> GetLotteryScheduleAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        var project = await _db.HousingProjects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);

        if (project is null) return null;

        return await BuildLotteryScheduleDetailDtoAsync(project, ct);
    }

    public async Task<List<LotteryParticipantDto>> GetEligibleParticipantsAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        return await _db.HousingApplications
            .AsNoTracking()
            .Include(a => a.Applicant)
            .Where(a => a.ProjectId == projectId
                        && BatchEligibleStatuses.Contains(a.ApplicationStatus)
                        && !a.IsViolation)
            .OrderBy(a => a.SubmittedAt)
            .Select(a => new LotteryParticipantDto
            {
                ApplicationId = a.ApplicationId,
                ApplicantId = a.ApplicantId,
                ApplicantName = a.Applicant != null ? a.Applicant.FullName : a.FullName,
                CitizenId = a.CitizenId,
                PriorityGroup = a.PriorityGroup,
                ApplicationStatus = a.ApplicationStatus,
                SubmittedAt = a.SubmittedAt
            })
            .ToListAsync(ct);
    }

    private async Task<LotteryScheduleDetailDto> BuildLotteryScheduleDetailDtoAsync(
        HousingProject project,
        CancellationToken ct)
    {
        var participants = await GetEligibleParticipantsAsync(project.Id, ct);

        string? supervisorName = project.LotterySupervisor?.FullName;
        if (project.LotterySupervisorId.HasValue && string.IsNullOrWhiteSpace(supervisorName))
        {
            supervisorName = await _db.Users.AsNoTracking()
                .Where(u => u.Id == project.LotterySupervisorId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(ct);
        }

        return new LotteryScheduleDetailDto
        {
            ProjectId = project.Id,
            ProjectName = project.ProjectName,
            LotteryDate = project.LotteryDate,
            LotteryLocation = project.LotteryLocation,
            LotteryType = project.LotteryType,
            LotteryDescription = project.LotteryDescription,
            IsLotteryApproved = project.IsLotteryApproved,
            LotteryApprovedAt = project.LotteryApprovedAt,
            AvailableUnits = project.AvailableUnits,
            TotalEligibleParticipants = participants.Count,
            EligibleParticipants = participants,
            SessionStatus = project.LotterySessionStatus,
            JoinCode = project.IsLotteryApproved == true ? project.LotteryJoinCode : null,
            SxdOnlineCount = LotteryHub.GetSxdOnlineCount(project.Id),
            SupervisorId = project.LotterySupervisorId,
            SupervisorName = supervisorName
        };
    }

    /// <summary>Đòi hỏi ≥1 SXD online trong Hub (Đ36.2.b NĐ 100/2024).</summary>
    private static void RequireSxdOnline(Guid projectId, string action)
    {
        if (LotteryHub.GetSxdOnlineCount(projectId) < 1)
            throw new InvalidOperationException(
                $"Không thể {action}: cần ít nhất 1 đại diện Sở Xây dựng đang online giám sát trong sảnh (NĐ 100/2024 Đ36.2.b).");
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

        var participants = await _db.HousingApplications
            .Include(a => a.PrincipleAgreement)
            .Where(a => a.ProjectId == projectId
                        && BatchEligibleStatuses.Contains(a.ApplicationStatus)
                        && !a.IsViolation)
            .OrderBy(a => a.SubmittedAt)
            .ToListAsync(ct);

        if (participants.Count == 0)
            throw new InvalidOperationException("Không có hồ sơ đủ điều kiện (APPROVED / APPROVED_BY_TIMEOUT) để bốc thăm.");

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
            var oldStatus = app.ApplicationStatus;
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
                OldStatus = oldStatus,
                NewStatus = ApplicationStatusConstants.ContractPending,
                Note = "Hồ sơ thuộc diện ưu tiên được phê duyệt trực tiếp, chuyển sang ký hợp đồng nguyên tắc.",
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
            var oldStatus = app.ApplicationStatus;
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
                OldStatus = oldStatus,
                NewStatus = ApplicationStatusConstants.ContractPending,
                Note = "Hồ sơ trúng bốc thăm, chuyển sang bước ký hợp đồng nguyên tắc.",
                ChangedAt = now
            });
        }

        foreach (var app in randomLosers)
        {
            var oldStatus = app.ApplicationStatus;
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
                OldStatus = oldStatus,
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

    /// <summary>[Mục 21 & 22] Xử lý bốc thăm tương tác thời gian thực với SemaphoreSlim Concurrency Lock (Row Lock 1 mili-giây).</summary>
    public async Task<LiveDrawResultDto> DrawUnitRealtimeAsync(
        Guid projectId,
        Guid applicantId,
        CancellationToken ct = default)
    {
        var semaphore = ProjectLocks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);

        try
        {
            var project = await _db.HousingProjects
                .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct)
                ?? throw new InvalidOperationException("Không tìm thấy dự án.");

            if (project.IsLotteryApproved != true)
                throw new InvalidOperationException("Lịch bốc thăm chưa được Sở phê duyệt.");

            if (!LotterySessionStatusConstants.CanDraw(project.LotterySessionStatus))
                throw new InvalidOperationException(
                    $"Chưa tới lúc bốc thăm. Trạng thái phiên hiện tại: {project.LotterySessionStatus ?? "(chưa mở)"}. Cần trạng thái Live.");

            RequireSxdOnline(projectId, "bốc thăm");

            var app = await _db.HousingApplications
                .Include(a => a.Applicant)
                .Include(a => a.PrincipleAgreement)
                .FirstOrDefaultAsync(a => a.ProjectId == projectId
                                          && a.ApplicantId == applicantId
                                          && BatchEligibleStatuses.Contains(a.ApplicationStatus)
                                          && !a.IsViolation, ct)
                ?? throw new InvalidOperationException("Hồ sơ không tồn tại hoặc chưa đủ điều kiện bốc thăm cho dự án này.");

            if (app.LotteryResult != null && app.LotteryResult != LotteryResultConstants.Pending)
            {
                throw new InvalidOperationException($"Bạn đã thực hiện bốc thăm trước đó. Kết quả: {app.LotteryResult}");
            }

            string resultStatus;
            string? slotCode = null;
            var now = DateTime.UtcNow;
            var oldStatus = app.ApplicationStatus;

            if (project.AvailableUnits > 0)
            {
                project.AvailableUnits--;
                bool isPriority = !string.IsNullOrWhiteSpace(app.PriorityGroup);
                resultStatus = isPriority ? LotteryResultConstants.PriorityWon : LotteryResultConstants.Won;

                var suffix = (DateTime.UtcNow.Ticks % 10000).ToString("D4");
                slotCode = $"LOT-{project.Id.ToString()[..4].ToUpper()}-{suffix}";

                app.LotteryResult = resultStatus;
                app.SlotCode = slotCode;
                app.ApplicationStatus = ApplicationStatusConstants.ContractPending;
                app.UpdatedAt = now;

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
                    ChangedBy = applicantId,
                    Action = ReviewActionConstants.LotteryWon,
                    OldStatus = oldStatus,
                    NewStatus = ApplicationStatusConstants.ContractPending,
                    Note = "Trúng bốc thăm live, chuyển sang ký hợp đồng nguyên tắc.",
                    ChangedAt = now
                });
            }
            else
            {
                resultStatus = LotteryResultConstants.Lost;
                app.LotteryResult = resultStatus;
                app.ApplicationStatus = ApplicationStatusConstants.LotteryLost;
                app.UpdatedAt = now;

                _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
                {
                    HistoryId = Guid.NewGuid(),
                    ApplicationId = app.ApplicationId,
                    ChangedBy = applicantId,
                    Action = ReviewActionConstants.LotteryLost,
                    OldStatus = oldStatus,
                    NewStatus = ApplicationStatusConstants.LotteryLost,
                    Note = "Trượt bốc thăm live (hết suất).",
                    ChangedAt = now
                });
            }

            await _db.SaveChangesAsync(ct);

            var liveResult = new LiveDrawResultDto
            {
                ProjectId = projectId,
                ApplicationId = app.ApplicationId,
                ApplicantId = app.ApplicantId,
                ApplicantName = app.Applicant != null ? app.Applicant.FullName : app.FullName,
                CitizenId = app.CitizenId,
                Result = resultStatus,
                SlotCode = slotCode,
                DrawnAt = DateTime.UtcNow,
                RemainingUnits = project.AvailableUnits,
                PriorityGroup = app.PriorityGroup
            };

            var groupName = LotteryHub.GetGroupName(projectId);
            await _hubContext.Clients.Group(groupName).ReceiveDrawResult(liveResult);

            return liveResult;
        }
        finally
        {
            semaphore.Release();
        }
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
                        && a.LotteryResult != null
                        && a.LotteryResult != LotteryResultConstants.Pending)
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

    public async Task<LotteryScheduleDetailDto> OpenLobbyAsync(
        Guid projectId, Guid actorId, CancellationToken ct = default)
    {
        var project = await RequireApprovedSessionAsync(projectId, ct);
        if (project.LotterySessionStatus is LotterySessionStatusConstants.Finished
            or LotterySessionStatusConstants.Published)
            throw new InvalidOperationException("Phiên đã kết thúc, không thể mở sảnh lại.");

        project.LotterySessionStatus = LotterySessionStatusConstants.WaitingLobby;
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await BroadcastStatusAsync(projectId, project.LotterySessionStatus);
        _logger.LogInformation("Lottery lobby opened for {ProjectId} by {Actor}", projectId, actorId);
        return await BuildLotteryScheduleDetailDtoAsync(project, ct);
    }

    public async Task<LotteryScheduleDetailDto> StartLiveAsync(
        Guid projectId, Guid actorId, CancellationToken ct = default)
    {
        var project = await RequireApprovedSessionAsync(projectId, ct);
        if (project.LotterySessionStatus is not (LotterySessionStatusConstants.WaitingLobby
            or LotterySessionStatusConstants.Scheduled))
            throw new InvalidOperationException(
                $"Chỉ mở Live từ WaitingLobby/Scheduled. Hiện tại: {project.LotterySessionStatus}");

        RequireSxdOnline(projectId, "bắt đầu Live");

        project.LotterySessionStatus = LotterySessionStatusConstants.Live;
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await BroadcastStatusAsync(projectId, project.LotterySessionStatus);
        _logger.LogInformation("Lottery LIVE started for {ProjectId} by {Actor}", projectId, actorId);
        return await BuildLotteryScheduleDetailDtoAsync(project, ct);
    }

    public async Task<LotteryScheduleDetailDto> FinishSessionAsync(
        Guid projectId, Guid actorId, CancellationToken ct = default)
    {
        var semaphore = ProjectLocks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        try
        {
            var project = await RequireApprovedSessionAsync(projectId, ct);
            if (project.LotterySessionStatus == LotterySessionStatusConstants.Published)
                throw new InvalidOperationException("Phiên đã công bố.");

            RequireSxdOnline(projectId, "kết thúc phiên");

            var now = DateTime.UtcNow;
            var pending = await _db.HousingApplications
                .Include(a => a.PrincipleAgreement)
                .Where(a => a.ProjectId == projectId
                            && BatchEligibleStatuses.Contains(a.ApplicationStatus)
                            && !a.IsViolation
                            && (a.LotteryResult == null || a.LotteryResult == LotteryResultConstants.Pending))
                .ToListAsync(ct);

            foreach (var app in pending)
            {
                var old = app.ApplicationStatus;
                app.LotteryResult = LotteryResultConstants.Lost;
                app.ApplicationStatus = ApplicationStatusConstants.LotteryLost;
                app.UpdatedAt = now;
                _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
                {
                    HistoryId = Guid.NewGuid(),
                    ApplicationId = app.ApplicationId,
                    ChangedBy = actorId,
                    Action = ReviewActionConstants.LotteryLost,
                    OldStatus = old,
                    NewStatus = ApplicationStatusConstants.LotteryLost,
                    Note = "Kết thúc phiên live — hồ sơ chưa bốc được ghi nhận trượt.",
                    ChangedAt = now
                });
            }

            var drawn = await _db.HousingApplications
                .AsNoTracking()
                .Where(a => a.ProjectId == projectId
                            && a.LotteryResult != null
                            && a.LotteryResult != LotteryResultConstants.Pending)
                .ToListAsync(ct);

            var winners = drawn.Count(a =>
                a.LotteryResult == LotteryResultConstants.Won
                || a.LotteryResult == LotteryResultConstants.PriorityWon);
            var priorityWon = drawn.Count(a => a.LotteryResult == LotteryResultConstants.PriorityWon);

            var results = drawn.Select(a => MapParticipant(
                a,
                a.LotteryResult!,
                !string.IsNullOrWhiteSpace(a.PriorityGroup))).ToList();
            results.AddRange(pending.Select(a => MapParticipant(a, LotteryResultConstants.Lost,
                !string.IsNullOrWhiteSpace(a.PriorityGroup))));

            var draw = new LotteryDraw
            {
                DrawId = Guid.NewGuid(),
                ProjectId = projectId,
                DrawnBy = actorId,
                DrawnAt = now,
                TotalUnits = winners,
                PriorityAllocated = priorityWon,
                RandomAllocated = Math.Max(0, winners - priorityWon),
                TotalParticipants = results.Count,
                RandomSeed = Environment.TickCount,
                ResultJson = JsonSerializer.Serialize(results.Select(r => new
                {
                    r.ApplicationId,
                    r.Result,
                    r.SlotCode,
                    r.IsPriority
                }))
            };
            _db.LotteryDraws.Add(draw);

            project.LotterySessionStatus = LotterySessionStatusConstants.Finished;
            project.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);
            await BroadcastStatusAsync(projectId, project.LotterySessionStatus);
            return await BuildLotteryScheduleDetailDtoAsync(project, ct);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<LotteryScheduleDetailDto> PublishSessionAsync(
        Guid projectId, Guid actorId, CancellationToken ct = default)
    {
        var project = await RequireApprovedSessionAsync(projectId, ct);
        if (project.LotterySessionStatus is not (LotterySessionStatusConstants.Finished
            or LotterySessionStatusConstants.Published))
            throw new InvalidOperationException("Chỉ công bố sau khi phiên Finished.");

        if (!project.LotterySupervisorId.HasValue)
            throw new InvalidOperationException(
                "Chưa ghi nhận SXD giám sát phiên — không thể công bố biên bản (NĐ 100/2024 Đ36.2.b).");

        project.LotterySessionStatus = LotterySessionStatusConstants.Published;
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var applicants = await _db.HousingApplications
            .Where(a => a.ProjectId == projectId && a.LotteryResult != null)
            .Select(a => a.ApplicantId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var id in applicants)
        {
            try
            {
                await _notificationService.SendAsync(
                    id,
                    "Kết quả bốc thăm đã công bố",
                    $"Kết quả phiên bốc thăm dự án '{project.ProjectName}' đã được công bố. Vui lòng xem trên App/Web.",
                    NotificationTypeConstants.LotteryResultPublished);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Notify publish failed for {UserId}", id);
            }
        }

        await BroadcastStatusAsync(projectId, project.LotterySessionStatus);
        _logger.LogInformation("Lottery session published for {ProjectId} by {Actor}", projectId, actorId);
        return await BuildLotteryScheduleDetailDtoAsync(project, ct);
    }

    public async Task<VerifyLotteryJoinCodeResultDto> VerifyJoinCodeAsync(
        Guid projectId, Guid userId, string? joinCode, bool isStaff, CancellationToken ct = default)
    {
        var project = await _db.HousingProjects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct)
            ?? throw new InvalidOperationException("Không tìm thấy dự án.");

        if (project.IsLotteryApproved != true)
            return new VerifyLotteryJoinCodeResultDto
            {
                Success = false,
                Message = "Lịch bốc thăm chưa được Sở phê duyệt.",
                SessionStatus = project.LotterySessionStatus
            };

        if (!LotterySessionStatusConstants.CanJoinLobby(project.LotterySessionStatus)
            && project.LotterySessionStatus != LotterySessionStatusConstants.Finished
            && project.LotterySessionStatus != LotterySessionStatusConstants.Published)
        {
            // Scheduled after approve vẫn cho verify OTP để chuẩn bị
            if (project.LotterySessionStatus != LotterySessionStatusConstants.Scheduled)
                return new VerifyLotteryJoinCodeResultDto
                {
                    Success = false,
                    Message = $"Phiên chưa mở sảnh. Trạng thái: {project.LotterySessionStatus}",
                    SessionStatus = project.LotterySessionStatus
                };
        }

        if (isStaff)
            return new VerifyLotteryJoinCodeResultDto
            {
                Success = true,
                Message = "Staff được vào sảnh giám sát.",
                SessionStatus = project.LotterySessionStatus
            };

        if (string.IsNullOrWhiteSpace(project.LotteryJoinCode)
            || !string.Equals(project.LotteryJoinCode.Trim(), joinCode?.Trim(), StringComparison.Ordinal))
        {
            return new VerifyLotteryJoinCodeResultDto
            {
                Success = false,
                Message = "Mã OTP không đúng.",
                SessionStatus = project.LotterySessionStatus
            };
        }

        var eligible = await _db.HousingApplications.AnyAsync(a =>
            a.ProjectId == projectId
            && a.ApplicantId == userId
            && BatchEligibleStatuses.Contains(a.ApplicationStatus)
            && !a.IsViolation, ct);

        // Cho phép cả người đã bốc (CONTRACT_PENDING / LOTTERY_LOST) vào xem lại
        var participated = await _db.HousingApplications.AnyAsync(a =>
            a.ProjectId == projectId
            && a.ApplicantId == userId
            && a.LotteryResult != null
            && a.LotteryResult != LotteryResultConstants.Pending, ct);

        if (!eligible && !participated)
            return new VerifyLotteryJoinCodeResultDto
            {
                Success = false,
                Message = "Bạn không nằm trong danh sách đủ điều kiện của phiên này.",
                SessionStatus = project.LotterySessionStatus
            };

        return new VerifyLotteryJoinCodeResultDto
        {
            Success = true,
            Message = "OTP hợp lệ — được vào sảnh.",
            SessionStatus = project.LotterySessionStatus
        };
    }

    public async Task RecordSupervisorAsync(Guid projectId, Guid sxdUserId, CancellationToken ct = default)
    {
        var project = await _db.HousingProjects
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);
        if (project == null) return;

        if (project.LotterySupervisorId.HasValue) return;

        project.LotterySupervisorId = sxdUserId;
        project.LotterySupervisedAt = DateTime.UtcNow;
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Lottery supervisor recorded for {ProjectId}: {SxdUserId}", projectId, sxdUserId);
    }

    private async Task<HousingProject> RequireApprovedSessionAsync(Guid projectId, CancellationToken ct)
    {
        var project = await _db.HousingProjects
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct)
            ?? throw new InvalidOperationException("Không tìm thấy dự án.");

        if (project.IsLotteryApproved != true || !project.LotteryDate.HasValue)
            throw new InvalidOperationException("Cần lịch bốc thăm đã được Sở phê duyệt.");

        return project;
    }

    private async Task BroadcastStatusAsync(Guid projectId, string status)
    {
        try
        {
            await _hubContext.Clients.Group(LotteryHub.GetGroupName(projectId))
                .ReceiveLotteryStatus(status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Broadcast lottery status failed for {ProjectId}", projectId);
        }
    }

    private static string GenerateJoinCode() =>
        Random.Shared.Next(100000, 999999).ToString();

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
