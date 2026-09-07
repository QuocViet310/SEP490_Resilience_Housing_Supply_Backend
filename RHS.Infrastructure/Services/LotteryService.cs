using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.HousingApplications;
using RHS.Application.DTOs.Lottery;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;
using RHS.Infrastructure.Helpers;
using RHS.Infrastructure.Hubs;

namespace RHS.Infrastructure.Services;

public class LotteryService : ILotteryService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly IHubContext<LotteryHub, ILotteryHubClient> _hubContext;
    private readonly IPolicyService _policyService;
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
        IPolicyService policyService,
        ILogger<LotteryService> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _hubContext = hubContext;
        _policyService = policyService;
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

        if (dto.LotteryDate == default)
            throw new InvalidOperationException("Vui lòng chọn ngày giờ bốc thăm.");

        if (string.IsNullOrWhiteSpace(dto.LotteryLocation))
            throw new InvalidOperationException("Vui lòng nhập địa điểm hoặc link kênh tham dự.");

        // Cho phép lệch đồng hồ nhẹ; lịch phải ở tương lai để người dân biết trước.
        if (dto.LotteryDate.ToUniversalTime() < DateTime.UtcNow.AddMinutes(-1))
            throw new InvalidOperationException("Thời gian bốc thăm phải ở tương lai.");

        // Danh sách tham gia bốc thăm phải cố định trước khi lên lịch. Nếu còn nhận hồ sơ thì
        // hoặc phải chặn oan người đang nộp, hoặc danh sách bị thay đổi sau khi đã công bố lịch.
        if (!project.ApplicationCloseDate.HasValue)
        {
            throw new InvalidOperationException(
                "Dự án chưa có hạn chót tiếp nhận hồ sơ. Hãy đóng đợt tiếp nhận trước khi đề xuất lịch bốc thăm.");
        }

        if (DateTime.UtcNow < project.ApplicationCloseDate.Value)
        {
            throw new InvalidOperationException(
                $"Dự án vẫn đang nhận hồ sơ đến {project.ApplicationCloseDate.Value:dd/MM/yyyy HH:mm}. " +
                "Chỉ đề xuất lịch bốc thăm sau khi đã hết hạn tiếp nhận và chốt danh sách, " +
                "để danh sách tham gia không thay đổi sau khi công bố lịch.");
        }

        // Căn cứ suất = đếm căn AVAILABLE − soft-hold
        await ProjectUnitSeatHelper.SyncAvailableUnitsAsync(_db, projectId, _logger, ct);

        if (project.AvailableUnits <= 0)
            throw new InvalidOperationException("Dự án đã hết suất — không cần đề xuất lịch bốc thăm.");

        var eligibleCount = await _db.HousingApplications.CountAsync(
            a => a.ProjectId == projectId
                 && BatchEligibleStatuses.Contains(a.ApplicationStatus)
                 && !a.IsViolation,
            ct);

        if (eligibleCount == 0)
            throw new InvalidOperationException(
                "Chưa có hồ sơ APPROVED còn lại để bốc thăm. Chỉ đề xuất lịch sau khi CĐT chốt vượt số căn.");

        if (eligibleCount <= project.AvailableUnits)
            throw new InvalidOperationException(
                $"Số hồ sơ chờ bốc thăm ({eligibleCount}) không vượt số căn còn lại ({project.AvailableUnits}). " +
                "Chỉ đề xuất lịch khi vượt số căn sau bước chốt của CĐT.");

        // Hệ thống chỉ hỗ trợ bốc thăm trực tuyến.
        dto.LotteryType = "ONLINE";

        project.LotteryDate = dto.LotteryDate;
        project.LotteryLocation = dto.LotteryLocation.Trim();
        project.LotteryType = "ONLINE";
        project.LotteryDescription = dto.LotteryDescription;
        project.IsLotteryApproved = false; // Chờ Admin/Sở duyệt
        project.LotterySessionStatus = LotterySessionStatusConstants.Scheduled;
        project.LotteryJoinCode = null;
        project.UpdatedAt = DateTime.UtcNow;

        if (dto.TotalUnits.HasValue && dto.TotalUnits.Value > 0)
        {
            if (dto.TotalUnits.Value > project.AvailableUnits)
                throw new InvalidOperationException(
                    $"Số căn mở bốc thăm không được vượt số căn còn lại ({project.AvailableUnits}).");
            // Không ghi đè AvailableUnits — TotalUnits chỉ là trần phiên bốc thăm (lưu ở draw lúc chạy).
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

        var notifTitle = "Sở đã phê duyệt & công bố lịch bốc thăm";
        var notifContent =
            $"Dự án '{project.ProjectName}': lịch bốc thăm chính thức vào lúc {project.LotteryDate:dd/MM/yyyy HH:mm} tại {project.LotteryLocation}. " +
            $"Hình thức: Trực tuyến (ONLINE). Mã OTP vào sảnh: {project.LotteryJoinCode}. " +
            "Lịch do chủ đầu tư đề xuất và đã được Sở Xây dựng phê duyệt.";

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
                ApplicationCode = $"HS-{a.SubmittedAt.Year}-{(a.ApplicationId.ToString().Substring(0, 4).ToUpper())}",
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

        if (project.IsLotteryApproved != true || !project.LotteryDate.HasValue)
            throw new InvalidOperationException(
                "Chỉ được chạy bốc thăm khi lịch ONLINE đã được Sở phê duyệt. Hãy dùng luồng sảnh Live.");

        var participants = await _db.HousingApplications
            .Include(a => a.PrincipleAgreement)
            .Where(a => a.ProjectId == projectId
                        && BatchEligibleStatuses.Contains(a.ApplicationStatus)
                        && !a.IsViolation)
            .OrderBy(a => a.SubmittedAt)
            .ToListAsync(ct);

        if (participants.Count == 0)
            throw new InvalidOperationException("Không có hồ sơ đủ điều kiện (APPROVED / APPROVED_BY_TIMEOUT) để bốc thăm.");

        // Re-run: hoàn căn + giải phóng soft-hold của lần trúng trước (nếu còn sót trong pool)
        var wonResults = new[]
        {
            LotteryResultConstants.Won,
            LotteryResultConstants.PriorityWon
        };
        var previousWinners = participants
            .Where(a => a.LotteryResult != null && wonResults.Contains(a.LotteryResult))
            .ToList();
        if (previousWinners.Count > 0)
        {
            foreach (var app in previousWinners)
            {
                if (app.ApartmentId.HasValue)
                {
                    var apt = await _db.Apartments.FirstOrDefaultAsync(
                        a => a.Id == app.ApartmentId.Value && a.ProjectId == projectId, ct);
                    if (apt != null
                        && string.Equals(apt.Status, ApartmentStatusConstants.Assigned, StringComparison.OrdinalIgnoreCase))
                    {
                        apt.Status = ApartmentStatusConstants.Available;
                    }
                    app.ApartmentId = null;
                }
                app.LotteryResult = LotteryResultConstants.Pending;
                app.SlotCode = null;
            }

            _logger.LogInformation(
                "Lottery re-run for project {ProjectId}: reset {Count} previous winner(s) before redraw.",
                projectId, previousWinners.Count);
        }

        // Căn cứ phân bổ = đếm căn AVAILABLE − soft-hold
        await ProjectUnitSeatHelper.SyncAvailableUnitsAsync(_db, projectId, _logger, ct);

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

        var seed = Environment.TickCount;
        var rng = new Random(seed);

        var priorityApps = participants
            .Where(a => !string.IsNullOrWhiteSpace(a.PriorityGroup))
            .OrderBy(_ => rng.Next())
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
            app.ApplicationStatus = ApplicationStatusConstants.DepositPending;
            app.UpdatedAt = now;
            results.Add(MapParticipant(app, LotteryResultConstants.PriorityWon, true));

            _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                HistoryId = Guid.NewGuid(),
                ApplicationId = app.ApplicationId,
                ChangedBy = drawnBy,
                Action = ReviewActionConstants.PriorityDirectApproval,
                OldStatus = oldStatus,
                NewStatus = ApplicationStatusConstants.DepositPending,
                Note = "Hồ sơ thuộc diện ưu tiên được phê duyệt trực tiếp, chuyển sang bước thanh toán Đợt 1 (thanh toán lần đầu, gồm cả tiền đặt cọc).",
                ChangedAt = now
            });
        }

        foreach (var app in priorityApps.Skip(priorityQuota))
        {
            // Ưu tiên dư → tham gia pool random cùng non-priority
            nonPriority.Add(app);
        }

        var remainingUnits = units - priorityWinners.Count;

        var shuffled = nonPriority.OrderBy(_ => rng.Next()).ToList();
        var randomWinners = shuffled.Take(remainingUnits).ToList();
        var randomLosers = shuffled.Skip(remainingUnits).ToList();

        foreach (var app in randomWinners)
        {
            winners.Add(app.ApplicationId);
            var oldStatus = app.ApplicationStatus;
            app.LotteryResult = LotteryResultConstants.Won;
            app.ApplicationStatus = ApplicationStatusConstants.DepositPending;
            app.UpdatedAt = now;
            results.Add(MapParticipant(app, LotteryResultConstants.Won, !string.IsNullOrWhiteSpace(app.PriorityGroup)));

            _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                HistoryId = Guid.NewGuid(),
                ApplicationId = app.ApplicationId,
                ChangedBy = drawnBy,
                Action = ReviewActionConstants.LotteryWon,
                OldStatus = oldStatus,
                NewStatus = ApplicationStatusConstants.DepositPending,
                Note = "Hồ sơ trúng bốc thăm, chuyển sang bước thanh toán Đợt 1 (thanh toán lần đầu, gồm cả tiền đặt cọc).",
                ChangedAt = now
            });
        }

        // Hồ sơ không trúng KHÔNG bị hủy: xếp vào Danh sách dự bị theo đúng thứ tự đã bốc,
        // để căn hộ bị trả lại về sau được chuyển quyền mua thay vì mở đợt bốc thăm mới.
        await AssignWaitlistRanksAsync(projectId, randomLosers, drawnBy, now, ct);

        foreach (var app in randomLosers)
        {
            results.Add(MapParticipant(app, LotteryResultConstants.Waitlist, !string.IsNullOrWhiteSpace(app.PriorityGroup)));
        }

        // Cập nhật HousingQuota RemainingSlots
        foreach (var quota in project.HousingQuotas)
        {
            var used = priorityWinners.Count(a =>
                string.Equals(a.PriorityGroup, quota.PriorityGroup, StringComparison.OrdinalIgnoreCase));
            quota.RemainingSlots = Math.Max(0, quota.AllocatedSlots - used);
        }

        // Trừ suất = soft-hold (CONTRACT_PENDING chưa gán căn) — sync từ đếm căn
        await ProjectUnitSeatHelper.SyncAvailableUnitsAsync(_db, projectId, _logger, ct);
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

    public async Task<LotteryLiveStateDto> GetLiveStateAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await _db.HousingProjects
            .AsNoTracking()
            .Include(p => p.Developer)
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct)
            ?? throw new InvalidOperationException("Không tìm thấy dự án.");

        var eligibleApps = await _db.HousingApplications
            .AsNoTracking()
            .Include(a => a.Applicant)
            .Where(a => a.ProjectId == projectId
                        && BatchEligibleStatuses.Contains(a.ApplicationStatus)
                        && !a.IsViolation)
            .OrderBy(a => a.SubmittedAt)
            .ToListAsync(ct);

        var wonStatuses = new[] { LotteryResultConstants.Won, LotteryResultConstants.PriorityWon };
        // Trúng đã chuyển CONTRACT_PENDING — không còn trong pool APPROVED, query riêng.
        var drawnWinners = await _db.HousingApplications
            .AsNoTracking()
            .Include(a => a.Applicant)
            .Include(a => a.Apartment)
            .Where(a => a.ProjectId == projectId
                        && a.LotteryResult != null
                        && wonStatuses.Contains(a.LotteryResult))
            .OrderBy(a => a.UpdatedAt ?? a.SubmittedAt)
            .ToListAsync(ct);

        var undrawnApps = eligibleApps
            .Where(a => a.LotteryResult == null || a.LotteryResult == LotteryResultConstants.Pending)
            .OrderByDescending(a => !string.IsNullOrWhiteSpace(a.PriorityGroup))
            .ThenBy(a => a.SubmittedAt)
            .ToList();

        var nextCandidateApp = undrawnApps.FirstOrDefault();

        int totalProjectApartments = await _db.Apartments.CountAsync(a => a.ProjectId == projectId, ct);
        int totalUnits = totalProjectApartments > 0
            ? totalProjectApartments
            : (project.AvailableUnits + drawnWinners.Count);

        int drawnUnitsCount = drawnWinners.Count;
        int remainingUnits = Math.Max(0, totalUnits - drawnUnitsCount);

        int sttCounter = 1;
        var recentWinners = drawnWinners.Select(a => new LiveDrawResultDto
        {
            ProjectId = projectId,
            ApplicationId = a.ApplicationId,
            ApplicationCode = GetApplicationCode(a),
            ApplicantId = a.ApplicantId,
            ApplicantName = a.Applicant != null ? a.Applicant.FullName : a.FullName,
            CitizenId = a.CitizenId,
            MaskedCitizenId = MaskCitizenId(a.CitizenId),
            Stt = sttCounter++,
            Result = a.LotteryResult!,
            SlotCode = a.SlotCode ?? a.Apartment?.UnitName,
            DrawnAt = a.UpdatedAt ?? DateTime.UtcNow,
            RemainingUnits = remainingUnits,
            PriorityGroup = a.PriorityGroup
        }).ToList();

        LiveDrawResultDto? latestDrawResult = recentWinners.LastOrDefault();

        LotteryParticipantDto? nextCandidate = null;
        if (nextCandidateApp != null)
        {
            nextCandidate = new LotteryParticipantDto
            {
                ApplicationId = nextCandidateApp.ApplicationId,
                ApplicationCode = GetApplicationCode(nextCandidateApp),
                ApplicantId = nextCandidateApp.ApplicantId,
                ApplicantName = nextCandidateApp.Applicant != null ? nextCandidateApp.Applicant.FullName : nextCandidateApp.FullName,
                CitizenId = nextCandidateApp.CitizenId,
                PriorityGroup = nextCandidateApp.PriorityGroup,
                ApplicationStatus = nextCandidateApp.ApplicationStatus,
                SubmittedAt = nextCandidateApp.SubmittedAt
            };
        }

        int priorityWinnersCount = drawnWinners.Count(w => w.LotteryResult == LotteryResultConstants.PriorityWon);
        int randomWinnersCount = drawnWinners.Count(w => w.LotteryResult == LotteryResultConstants.Won);
        int undrawnParticipantsCount = undrawnApps.Count;
        int totalEligible = undrawnApps.Count + drawnWinners.Count;
        double winRatePercentage = totalEligible > 0
            ? Math.Min(100.0, Math.Round((double)totalUnits / totalEligible * 100.0, 1))
            : 0.0;

        // Build Khu vực 3: Thống kê quỹ căn của dự án
        double overallPct = totalUnits > 0 ? Math.Round((double)remainingUnits / totalUnits * 100.0, 1) : 0.0;

        var projectFundStat = new ApartmentFundQuotaStatDto
        {
            CategoryName = "Quỹ căn dự án",
            TotalUnits = totalUnits,
            RemainingUnits = remainingUnits,
            AssignedUnits = drawnUnitsCount,
            RemainingPercentage = overallPct
        };

        var projectApartments = await _db.Apartments
            .Include(a => a.ApartmentType)
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId)
            .ToListAsync(ct);

        var apartmentFundStats = new List<ApartmentFundQuotaStatDto>();

        if (projectApartments.Count > 0)
        {
            var grouped = projectApartments
                .GroupBy(a => a.ApartmentTypeId)
                .ToList();

            foreach (var group in grouped)
            {
                var typeId = group.Key;
                var sampleApt = group.First();
                string categoryName = sampleApt.ApartmentType?.TypeName
                    ?? (!string.IsNullOrWhiteSpace(sampleApt.Description) ? sampleApt.Description.Trim() : "Loại căn");
                string? typeCode = sampleApt.ApartmentType?.TypeCode;

                int totalInGroup = group.Count();
                int remainingInGroup = await ProjectUnitSeatHelper.GetAvailableUnitsByTypeAsync(_db, projectId, typeId, ct);
                int assignedInGroup = Math.Max(0, totalInGroup - remainingInGroup);
                double pct = totalInGroup > 0 ? Math.Round((double)remainingInGroup / totalInGroup * 100.0, 1) : 0.0;

                apartmentFundStats.Add(new ApartmentFundQuotaStatDto
                {
                    ApartmentTypeId = typeId,
                    ApartmentTypeCode = typeCode,
                    CategoryName = categoryName,
                    TotalUnits = totalInGroup,
                    RemainingUnits = remainingInGroup,
                    AssignedUnits = assignedInGroup,
                    RemainingPercentage = pct
                });
            }
        }
        else
        {
            apartmentFundStats.Add(projectFundStat);
        }

        var devName = project.Developer?.FullName ?? "Chủ đầu tư";

        return new LotteryLiveStateDto
        {
            ProjectId = project.Id,
            ProjectName = project.ProjectName,
            DeveloperName = devName,
            SessionStatus = project.LotterySessionStatus ?? LotterySessionStatusConstants.Scheduled,
            TotalUnits = totalUnits,
            DrawnUnitsCount = drawnUnitsCount,
            RemainingUnits = remainingUnits,
            TotalEligibleParticipants = totalEligible,
            SxdOnlineCount = LotteryHub.GetSxdOnlineCount(projectId),
            LobbyCount = LotteryHub.GetLobbyCount(projectId),
            PriorityWinnersCount = priorityWinnersCount,
            RandomWinnersCount = randomWinnersCount,
            UndrawnParticipantsCount = undrawnParticipantsCount,
            WinRatePercentage = winRatePercentage,
            NextCandidate = nextCandidate,
            LatestDrawResult = latestDrawResult,
            RecentWinners = recentWinners,
            ProjectApartmentFundStat = projectFundStat,
            ApartmentFundStats = apartmentFundStats
        };
    }

    /// <summary>CĐT kích hoạt bốc 1 lượt tiếp theo ("Bốc tiếp").</summary>
    public async Task<LiveDrawResultDto> DrawNextTurnAsync(
        Guid projectId,
        Guid actorId,
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

            if (project.LotterySessionStatus != LotterySessionStatusConstants.Live)
                throw new InvalidOperationException(
                    $"Chưa tới lúc bốc thăm. Trạng thái phiên hiện tại: {project.LotterySessionStatus ?? "(chưa mở)"}. Cần trạng thái Live.");

            RequireSxdOnline(projectId, "bốc thăm");

            var undrawnApps = await _db.HousingApplications
                .Include(a => a.Applicant)
                .Include(a => a.DesiredApartmentType)
                .Include(a => a.PrincipleAgreement)
                .Where(a => a.ProjectId == projectId
                            && BatchEligibleStatuses.Contains(a.ApplicationStatus)
                            && !a.IsViolation
                            && (a.LotteryResult == null || a.LotteryResult == LotteryResultConstants.Pending))
                .OrderBy(a => a.SubmittedAt)
                .ToListAsync(ct);

            if (undrawnApps.Count == 0)
                throw new InvalidOperationException("Tất cả hồ sơ đủ điều kiện đều đã hoàn thành bốc thăm.");

            // 1. Tìm TẤT CẢ ứng viên ưu tiên có loại căn mong muốn còn suất
            var validPriorityApps = new List<HousingApplication>();
            foreach (var a in undrawnApps.Where(a => !string.IsNullOrWhiteSpace(a.PriorityGroup)))
            {
                int availForType = await ProjectUnitSeatHelper.GetAvailableUnitsByTypeAsync(_db, projectId, a.DesiredApartmentTypeId, ct);
                if (availForType > 0)
                {
                    validPriorityApps.Add(a);
                }
            }

            HousingApplication? app = null;
            if (validPriorityApps.Count > 0)
            {
                // Chọn NGẪU NHIÊN 1 ứng viên trong danh sách ưu tiên còn loại căn
                app = validPriorityApps[Random.Shared.Next(validPriorityApps.Count)];
            }
            else
            {
                // 2. Nếu không có ứng viên ưu tiên còn loại căn khả dụng -> Tìm TẤT CẢ ứng viên thường có loại căn mong muốn còn suất
                var validNonPriority = new List<HousingApplication>();
                foreach (var a in undrawnApps.Where(a => string.IsNullOrWhiteSpace(a.PriorityGroup)))
                {
                    int availForType = await ProjectUnitSeatHelper.GetAvailableUnitsByTypeAsync(_db, projectId, a.DesiredApartmentTypeId, ct);
                    if (availForType > 0)
                    {
                        validNonPriority.Add(a);
                    }
                }

                if (validNonPriority.Count > 0)
                {
                    // Chọn NGẪU NHIÊN 1 ứng viên trong danh sách thường còn loại căn
                    app = validNonPriority[Random.Shared.Next(validNonPriority.Count)];
                }
            }

            // Nếu không còn ứng viên nào có loại căn mong muốn còn suất -> Xếp các ứng viên còn lại vào Danh sách chờ (Waitlist)
            if (app == null)
            {
                var nowExhausted = DateTime.UtcNow;

                // Thứ hạng dự bị tiếp tục theo thứ tự bốc ngẫu nhiên của chính phiên này,
                // dùng cùng dãy số với các đợt trước để chỉ có một Danh sách dự bị cho cả dự án.
                var remainingInDrawOrder = undrawnApps
                    .OrderBy(_ => Random.Shared.Next())
                    .ToList();

                var assigned = await AssignWaitlistRanksAsync(
                    projectId, remainingInDrawOrder, actorId, nowExhausted, ct);

                await _db.SaveChangesAsync(ct);

                throw new InvalidOperationException(
                    "Các loại căn hộ mà hồ sơ còn lại đăng ký đều đã hết suất. " +
                    $"{assigned} hồ sơ còn lại đã được xếp vào Danh sách dự bị theo thứ tự bốc thăm — " +
                    "khi có căn bị trả lại, hệ thống sẽ chuyển quyền mua theo thứ tự này.");
            }

            var applicantId = app.ApplicantId;
            string resultStatus;
            string? slotCode = null;
            var now = DateTime.UtcNow;
            var oldStatus = app.ApplicationStatus;

            int remainingForTypeBefore = await ProjectUnitSeatHelper.GetAvailableUnitsByTypeAsync(_db, projectId, app.DesiredApartmentTypeId, ct);
            var remainingTotalBefore = await ProjectUnitSeatHelper.SyncAvailableUnitsAsync(_db, projectId, _logger, ct);

            if (remainingForTypeBefore > 0 && remainingTotalBefore > 0)
            {
                bool isPriority = !string.IsNullOrWhiteSpace(app.PriorityGroup);
                resultStatus = isPriority ? LotteryResultConstants.PriorityWon : LotteryResultConstants.Won;

                string typeName = app.DesiredApartmentType?.TypeName ?? "loại căn đã chọn";
                MarkWonAwaitingApartment(app, resultStatus, actorId, oldStatus, now,
                    $"Trúng bốc thăm live ({typeName}). Chờ CĐT cấp căn sau.");

                await ProjectUnitSeatHelper.SyncAvailableUnitsAsync(_db, projectId, _logger, ct);
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
                    ChangedBy = actorId,
                    Action = ReviewActionConstants.LotteryLost,
                    OldStatus = oldStatus,
                    NewStatus = ApplicationStatusConstants.LotteryLost,
                    Note = "Trượt bốc thăm live (loại căn hộ đăng ký đã hết suất).",
                    ChangedAt = now
                });
            }

            await _db.SaveChangesAsync(ct);

            try
            {
                if (resultStatus == LotteryResultConstants.Won || resultStatus == LotteryResultConstants.PriorityWon)
                {
                    await _notificationService.SendAsync(
                        applicantId,
                        "Trúng bốc thăm — chờ Chủ đầu tư chọn căn",
                        "Bạn đã trúng bốc thăm. Chủ đầu tư sẽ chọn căn cho hồ sơ của bạn; sau khi được cấp căn hãy xem và ký hợp đồng mua bán nhà ở xã hội.",
                        NotificationTypeConstants.ContractPending);
                }
                else if (resultStatus == LotteryResultConstants.Lost)
                {
                    await _notificationService.SendAsync(
                        applicantId,
                        "Kết quả bốc thăm",
                        "Rất tiếc, hồ sơ của bạn không trúng trong phiên bốc thăm này.",
                        NotificationTypeConstants.LotteryResultPublished);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi gửi thông báo kết quả live draw cho user {UserId}", applicantId);
            }

            var wonStatuses = new[] { LotteryResultConstants.Won, LotteryResultConstants.PriorityWon };
            var wonCount = await _db.HousingApplications.CountAsync(a =>
                a.ProjectId == projectId
                && a.LotteryResult != null
                && wonStatuses.Contains(a.LotteryResult), ct);

            var liveResult = new LiveDrawResultDto
            {
                ProjectId = projectId,
                ApplicationId = app.ApplicationId,
                ApplicationCode = GetApplicationCode(app),
                ApplicantId = app.ApplicantId,
                ApplicantName = app.Applicant != null ? app.Applicant.FullName : app.FullName,
                CitizenId = app.CitizenId,
                MaskedCitizenId = MaskCitizenId(app.CitizenId),
                Stt = wonCount,
                Result = resultStatus,
                SlotCode = slotCode,
                DrawnAt = DateTime.UtcNow,
                RemainingUnits = project.AvailableUnits,
                PriorityGroup = app.PriorityGroup
            };

            var groupName = LotteryHub.GetGroupName(projectId);
            await _hubContext.Clients.Group(groupName).ReceiveDrawResult(liveResult);

            var updatedState = await GetLiveStateAsync(projectId, ct);
            await _hubContext.Clients.Group(groupName).ReceiveLiveState(updatedState);

            return liveResult;
        }
        finally
        {
            semaphore.Release();
        }
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
                .Include(a => a.DesiredApartmentType)
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

            int remainingForTypeBefore = await ProjectUnitSeatHelper.GetAvailableUnitsByTypeAsync(_db, projectId, app.DesiredApartmentTypeId, ct);
            var remainingTotalBefore = await ProjectUnitSeatHelper.SyncAvailableUnitsAsync(_db, projectId, _logger, ct);

            if (remainingForTypeBefore > 0 && remainingTotalBefore > 0)
            {
                bool isPriority = !string.IsNullOrWhiteSpace(app.PriorityGroup);
                resultStatus = isPriority ? LotteryResultConstants.PriorityWon : LotteryResultConstants.Won;

                string typeName = app.DesiredApartmentType?.TypeName ?? "loại căn đã chọn";
                MarkWonAwaitingApartment(app, resultStatus, applicantId, oldStatus, now,
                    $"Trúng bốc thăm live ({typeName}). Chờ Chủ đầu tư chọn căn cụ thể trước khi ký HĐ.");

                await ProjectUnitSeatHelper.SyncAvailableUnitsAsync(_db, projectId, _logger, ct);
            }
            else
            {
                resultStatus = LotteryResultConstants.Waitlist;
                app.LotteryResult = resultStatus;
                app.ApplicationStatus = ApplicationStatusConstants.Waitlist;
                app.UpdatedAt = now;

                int existingWaitlistCount = await _db.HousingApplications.CountAsync(
                    a => a.ProjectId == projectId
                         && a.DesiredApartmentTypeId == app.DesiredApartmentTypeId
                         && a.WaitlistNumber.HasValue, ct);

                app.WaitlistNumber = existingWaitlistCount + 1;

                _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
                {
                    HistoryId = Guid.NewGuid(),
                    ApplicationId = app.ApplicationId,
                    ChangedBy = applicantId,
                    Action = ReviewActionConstants.LotteryLost,
                    OldStatus = oldStatus,
                    NewStatus = ApplicationStatusConstants.Waitlist,
                    Note = $"Trượt bốc thăm live (Loại căn hộ đã chọn đã hết suất). Đã được xếp vào Danh sách dự bị (Waitlist #{app.WaitlistNumber}).",
                    ChangedAt = now
                });
            }

            await _db.SaveChangesAsync(ct);

            try
            {
                if (resultStatus == LotteryResultConstants.Won || resultStatus == LotteryResultConstants.PriorityWon)
                {
                    await _notificationService.SendAsync(
                        applicantId,
                        "Trúng bốc thăm — chờ Chủ đầu tư chọn căn",
                        "Bạn đã trúng bốc thăm. Chủ đầu tư sẽ chọn căn cho hồ sơ của bạn; sau khi được cấp căn hãy xem và ký hợp đồng mua bán nhà ở xã hội.",
                        NotificationTypeConstants.ContractPending);
                }
                else if (resultStatus == LotteryResultConstants.Lost)
                {
                    await _notificationService.SendAsync(
                        applicantId,
                        "Kết quả bốc thăm",
                        "Rất tiếc, hồ sơ của bạn không trúng trong phiên bốc thăm này.",
                        NotificationTypeConstants.LotteryResultPublished);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi gửi thông báo kết quả live draw cho user {UserId}", applicantId);
            }

            var wonStatuses = new[] { LotteryResultConstants.Won, LotteryResultConstants.PriorityWon };
            var wonCount = await _db.HousingApplications.CountAsync(a =>
                a.ProjectId == projectId
                && a.LotteryResult != null
                && wonStatuses.Contains(a.LotteryResult), ct);

            var liveResult = new LiveDrawResultDto
            {
                ProjectId = projectId,
                ApplicationId = app.ApplicationId,
                ApplicationCode = GetApplicationCode(app),
                ApplicantId = app.ApplicantId,
                ApplicantName = app.Applicant != null ? app.Applicant.FullName : app.FullName,
                CitizenId = app.CitizenId,
                MaskedCitizenId = MaskCitizenId(app.CitizenId),
                Stt = wonCount,
                Result = resultStatus,
                SlotCode = slotCode,
                DrawnAt = DateTime.UtcNow,
                RemainingUnits = project.AvailableUnits,
                PriorityGroup = app.PriorityGroup
            };

            var groupName = LotteryHub.GetGroupName(projectId);
            await _hubContext.Clients.Group(groupName).ReceiveDrawResult(liveResult);

            var updatedState = await GetLiveStateAsync(projectId, ct);
            await _hubContext.Clients.Group(groupName).ReceiveLiveState(updatedState);

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
            or LotterySessionStatusConstants.Scheduled
            or LotterySessionStatusConstants.Paused))
            throw new InvalidOperationException(
                $"Chỉ mở Live từ WaitingLobby/Scheduled/Paused. Hiện tại: {project.LotterySessionStatus}");

        RequireSxdOnline(projectId, "bắt đầu Live");

        project.LotterySessionStatus = LotterySessionStatusConstants.Live;
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await BroadcastStatusAsync(projectId, project.LotterySessionStatus);
        _logger.LogInformation("Lottery LIVE started for {ProjectId} by {Actor}", projectId, actorId);

        try
        {
            var liveState = await GetLiveStateAsync(projectId, ct);
            await _hubContext.Clients.Group(LotteryHub.GetGroupName(projectId)).ReceiveLiveState(liveState);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Broadcast live state on StartLive failed for {ProjectId}", projectId);
        }

        return await BuildLotteryScheduleDetailDtoAsync(project, ct);
    }

    public async Task<LotteryScheduleDetailDto> PauseLiveAsync(
        Guid projectId, Guid actorId, CancellationToken ct = default)
    {
        var project = await RequireApprovedSessionAsync(projectId, ct);
        if (project.LotterySessionStatus != LotterySessionStatusConstants.Live)
            throw new InvalidOperationException(
                $"Chỉ có thể tạm dừng khi phiên đang Live. Hiện tại: {project.LotterySessionStatus}");

        project.LotterySessionStatus = LotterySessionStatusConstants.Paused;
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await BroadcastStatusAsync(projectId, project.LotterySessionStatus);
        _logger.LogInformation("Lottery session PAUSED for {ProjectId} by {Actor}", projectId, actorId);

        try
        {
            var liveState = await GetLiveStateAsync(projectId, ct);
            await _hubContext.Clients.Group(LotteryHub.GetGroupName(projectId)).ReceiveLiveState(liveState);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Broadcast live state on PauseLive failed for {ProjectId}", projectId);
        }

        return await BuildLotteryScheduleDetailDtoAsync(project, ct);
    }

    public async Task<LotteryScheduleDetailDto> ResumeLiveAsync(
        Guid projectId, Guid actorId, CancellationToken ct = default)
    {
        return await StartLiveAsync(projectId, actorId, ct);
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

            // Hồ sơ chưa bốc tới khi phiên kết thúc cũng vào Danh sách dự bị, không bị hủy.
            var pendingInDrawOrder = pending.OrderBy(_ => Random.Shared.Next()).ToList();
            await AssignWaitlistRanksAsync(projectId, pendingInDrawOrder, actorId, now, ct);

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
            results.AddRange(pending.Select(a => MapParticipant(a, LotteryResultConstants.Waitlist,
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

    /// <summary>
    /// Công bố trúng: giữ suất (CONTRACT_PENDING + soft-hold), không gán căn.
    /// CĐT chọn căn sau qua POST /api/housing-applications/{id}/assign-apartment.
    /// </summary>
    private void MarkWonAwaitingApartment(
        HousingApplication app,
        string resultStatus,
        Guid changedBy,
        string oldStatus,
        DateTime now,
        string note)
    {
        app.LotteryResult = resultStatus;
        app.SlotCode = null;
        app.ApplicationStatus = ApplicationStatusConstants.LotteryWon;
        app.UpdatedAt = now;

        _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            HistoryId = Guid.NewGuid(),
            ApplicationId = app.ApplicationId,
            ChangedBy = changedBy,
            Action = ReviewActionConstants.LotteryWon,
            OldStatus = oldStatus,
            NewStatus = ApplicationStatusConstants.LotteryWon,
            Note = note,
            ChangedAt = now
        });
    }

    private static string GetApplicationCode(HousingApplication app)
    {
        var year = app.SubmittedAt != default ? app.SubmittedAt.Year : DateTime.UtcNow.Year;
        var shortCode = app.ApplicationId.ToString()[..4].ToUpper();
        return $"HS-{year}-{shortCode}";
    }

    private static string MaskCitizenId(string? citizenId)
    {
        if (string.IsNullOrWhiteSpace(citizenId)) return string.Empty;
        var trimmed = citizenId.Trim();
        if (trimmed.Length < 6) return trimmed;
        var prefix = trimmed[..3];
        var suffix = trimmed[^3..];
        var stars = new string('*', Math.Max(3, trimmed.Length - 6));
        return $"{prefix}{stars}{suffix}";
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
        WaitlistNumber = app.WaitlistNumber,
        IsPriority = isPriority
    };

    /// <summary>
    /// Xếp các hồ sơ không trúng vào Danh sách dự bị theo MỘT dãy thứ hạng duy nhất cho cả dự án
    /// (Thứ tự 1, 2, 3...). Thứ tự truyền vào chính là thứ tự đã bốc công khai, nên thứ hạng dự bị
    /// luôn truy được về biên bản phiên bốc thăm chứ không phải hệ thống tự sắp lại.
    /// Hồ sơ đã có thứ hạng thì giữ nguyên, không đánh số lại.
    /// </summary>
    private async Task<int> AssignWaitlistRanksAsync(
        Guid projectId,
        IReadOnlyList<HousingApplication> orderedApps,
        Guid actorId,
        DateTime now,
        CancellationToken ct)
    {
        if (orderedApps.Count == 0) return 0;

        var maxRank = await _db.HousingApplications
            .Where(a => a.ProjectId == projectId && a.WaitlistNumber.HasValue)
            .MaxAsync(a => (int?)a.WaitlistNumber, ct) ?? 0;

        var assigned = 0;

        foreach (var app in orderedApps)
        {
            if (app.WaitlistNumber.HasValue) continue;

            var rank = maxRank + assigned + 1;
            var oldStatus = app.ApplicationStatus;

            app.WaitlistNumber = rank;
            app.LotteryResult = LotteryResultConstants.Waitlist;
            app.ApplicationStatus = ApplicationStatusConstants.Waitlist;
            app.UpdatedAt = now;

            _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                HistoryId = Guid.NewGuid(),
                ApplicationId = app.ApplicationId,
                ChangedBy = actorId,
                Action = ReviewActionConstants.WaitlistAssigned,
                OldStatus = oldStatus,
                NewStatus = ApplicationStatusConstants.Waitlist,
                Note = $"Không trúng đợt bốc thăm chính thức. Hồ sơ không bị hủy mà được xếp vào " +
                       $"Danh sách dự bị thứ tự #{rank}. Khi có căn hộ bị trả lại do hủy hợp đồng " +
                       "hoặc không nộp tiền đúng hạn, hệ thống gọi lần lượt theo thứ tự này.",
                ChangedAt = now
            });

            assigned++;
        }

        return assigned;
    }

    /// <summary>
    /// Đường đôn Danh sách dự bị DUY NHẤT của hệ thống. Mọi nguồn suất hoàn lại
    /// (hủy hợp đồng, cưỡng chế thu hồi, quá hạn nộp tiền, người được đôn bỏ suất)
    /// đều phải gọi hàm này để thứ hạng và hạn xác nhận chỉ có một cách hiểu.
    /// </summary>
    /// <param name="desiredApartmentTypeId">
    /// Chỉ gọi người có nguyện vọng đúng loại căn được hoàn lại. Người bị bỏ qua GIỮ NGUYÊN
    /// thứ hạng để được gọi khi có căn đúng loại nguyện vọng của họ.
    /// </param>
    /// <param name="releasedApartmentId">Căn cụ thể được hoàn lại, nếu có thì gán luôn cho người được đôn.</param>
    public async Task<HousingApplication?> PromoteNextWaitlistApplicantAsync(
        Guid projectId,
        Guid? desiredApartmentTypeId,
        Guid? releasedApartmentId = null,
        CancellationToken ct = default)
    {
        var semaphore = ProjectLocks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);

        try
        {
            Apartment? releasedApartment = null;
            if (releasedApartmentId.HasValue && releasedApartmentId.Value != Guid.Empty)
            {
                releasedApartment = await _db.Apartments
                    .Include(a => a.ApartmentType)
                    .FirstOrDefaultAsync(a => a.Id == releasedApartmentId.Value && a.ProjectId == projectId, ct);
            }

            // Căn hoàn lại quyết định loại căn cần gọi; nếu không có căn cụ thể thì dùng tham số.
            var targetTypeId = releasedApartment?.ApartmentTypeId ?? desiredApartmentTypeId;

            var candidates = await _db.HousingApplications
                .Include(a => a.Applicant)
                .Include(a => a.DesiredApartmentType)
                .Where(a => a.ProjectId == projectId
                            && a.ApplicationStatus == ApplicationStatusConstants.Waitlist
                            && a.WaitlistNumber.HasValue
                            && !a.IsViolation)
                .OrderBy(a => a.WaitlistNumber)
                .ToListAsync(ct);

            // Người chưa chọn nguyện vọng loại căn thì nhận được mọi loại.
            // Khi biết căn cụ thể được hoàn lại thì phải qua cùng bộ ràng buộc như CĐT gán căn tay,
            // nếu không thì một căn thuộc quỹ ưu tiên có thể bị đôn cho hồ sơ không thuộc nhóm ưu tiên.
            var nextCandidate = candidates.FirstOrDefault(a =>
                releasedApartment != null
                    ? ApartmentAssignmentGate.IsAssignable(a, releasedApartment)
                    : targetTypeId == null
                        || a.DesiredApartmentTypeId == null
                        || a.DesiredApartmentTypeId == targetTypeId);

            if (nextCandidate == null)
            {
                _logger.LogInformation(
                    "Danh sách dự bị dự án {ProjectId} không có ai phù hợp loại căn {TypeId} ({Total} hồ sơ dự bị).",
                    projectId, targetTypeId, candidates.Count);
                return null;
            }

            var confirmHours = await _policyService.GetValueAsync(PolicyKeys.WaitlistConfirmHours, 48, ct);
            var now = DateTime.UtcNow;
            var oldStatus = nextCandidate.ApplicationStatus;

            // Có căn cụ thể thì vào ngay bước nộp tiền như người trúng bốc thăm bình thường;
            // chưa có căn cụ thể thì chờ CĐT cấp căn (LOTTERY_WON).
            nextCandidate.LotteryResult = LotteryResultConstants.Won;
            nextCandidate.ApplicationStatus = releasedApartment != null
                ? ApplicationStatusConstants.DepositPending
                : ApplicationStatusConstants.LotteryWon;
            nextCandidate.WaitlistPromotedAt = now;
            nextCandidate.DepositDeadline = now.AddHours(confirmHours);
            nextCandidate.UpdatedAt = now;

            if (releasedApartment != null)
            {
                nextCandidate.ApartmentId = releasedApartment.Id;
                releasedApartment.Status = ApartmentStatusConstants.Assigned;
                releasedApartment.UpdatedAt = now;
            }

            var typeName = releasedApartment?.UnitName
                ?? nextCandidate.DesiredApartmentType?.TypeName
                ?? "loại căn đã đăng ký nguyện vọng";
            var deadlineText = nextCandidate.DepositDeadline!.Value.ToString("dd/MM/yyyy HH:mm");

            _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                HistoryId = Guid.NewGuid(),
                ApplicationId = nextCandidate.ApplicationId,
                ChangedBy = nextCandidate.ApplicantId,
                Action = ReviewActionConstants.WaitlistPromoted,
                OldStatus = oldStatus,
                NewStatus = nextCandidate.ApplicationStatus,
                Note = $"Được đôn từ Danh sách dự bị #{nextCandidate.WaitlistNumber} lên suất trúng mua ({typeName}) " +
                       $"do có căn hoàn lại. Không mở đợt bốc thăm mới. " +
                       $"Hạn xác nhận nộp tiền đợt 1: {deadlineText} ({confirmHours} giờ). " +
                       "Quá hạn thì mất suất và hệ thống gọi người kế tiếp.",
                ChangedAt = now
            });

            await _db.SaveChangesAsync(ct);

            await ProjectUnitSeatHelper.SyncAvailableUnitsAsync(_db, projectId, _logger, ct);

            try
            {
                await _notificationService.SendAsync(
                    nextCandidate.ApplicantId,
                    "Bạn đã được đôn từ Danh sách dự bị lên suất trúng mua NOXH",
                    $"Do có căn hộ ({typeName}) bị trả lại, hồ sơ của bạn (dự bị số {nextCandidate.WaitlistNumber}) " +
                    $"đã được chuyển quyền mua. Vui lòng xác nhận và hoàn tất nộp tiền đợt 1 trước {deadlineText} " +
                    $"(trong {confirmHours} giờ). Quá hạn, suất sẽ được chuyển cho người kế tiếp trong danh sách.",
                    NotificationTypeConstants.ContractPending);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi gửi thông báo đôn Waitlist cho applicant {ApplicantId}", nextCandidate.ApplicantId);
            }

            try
            {
                var groupName = LotteryHub.GetGroupName(projectId);
                var updatedState = await GetLiveStateAsync(projectId, ct);
                await _hubContext.Clients.Group(groupName).ReceiveLiveState(updatedState);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi broadcast live state sau khi đôn Waitlist dự án {ProjectId}", projectId);
            }

            _logger.LogInformation(
                "Đã đôn ứng viên {ApplicantId} (dự bị #{WaitlistNum}) lên suất trúng mua dự án {ProjectId}, hạn xác nhận {Deadline}.",
                nextCandidate.ApplicantId, nextCandidate.WaitlistNumber, projectId, nextCandidate.DepositDeadline);

            return nextCandidate;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<List<ApplicationSummaryResponseDto>> GetWaitlistAsync(
        Guid projectId,
        Guid? desiredApartmentTypeId = null,
        CancellationToken ct = default)
    {
        var query = _db.HousingApplications
            .AsNoTracking()
            .Include(a => a.HousingProject)
            .Include(a => a.DesiredApartmentType)
            .Where(a => a.ProjectId == projectId
                        && (a.ApplicationStatus == ApplicationStatusConstants.Waitlist || a.LotteryResult == LotteryResultConstants.Waitlist)
                        && a.WaitlistNumber.HasValue);

        if (desiredApartmentTypeId.HasValue)
        {
            query = query.Where(a => a.DesiredApartmentTypeId == desiredApartmentTypeId.Value);
        }

        return await query
            .OrderBy(a => a.WaitlistNumber)
            .Select(x => new ApplicationSummaryResponseDto
            {
                ApplicationId     = x.ApplicationId,
                ProjectId         = x.ProjectId,
                ProjectName       = x.HousingProject.ProjectName,
                ApplicantId       = x.ApplicantId,
                ApplicantFullName = x.FullName,
                CitizenId         = x.CitizenId,
                ApplicationStatus = x.ApplicationStatus,
                CreatedAt         = x.CreatedAt,
                SubmittedAt       = x.SubmittedAt,
                FinalDecisionDate = x.FinalDecisionDate,
                HousingStatus     = x.HousingStatus,
                MaritalStatus     = x.MaritalStatus,
                HouseholdMembersCount = x.HouseholdMembersCount,
                PriorityGroup     = x.PriorityGroup,
                ReceiptUrl        = x.ReceiptUrl,
                DocumentCount     = x.Documents.Count,
                IsViolation       = x.IsViolation,
                ViolationReason   = x.ViolationReason,
                DesiredApartmentTypeId = x.DesiredApartmentTypeId,
                DesiredApartmentType   = x.DesiredApartmentType != null ? x.DesiredApartmentType.TypeCode : null,
                DesiredApartmentTypeLabel = x.DesiredApartmentType != null ? x.DesiredApartmentType.TypeName : null,
                WaitlistNumber    = x.WaitlistNumber,
                WaitlistPromotedAt = x.WaitlistPromotedAt,
                DepositDeadline   = x.DepositDeadline
            })
            .ToListAsync(ct);
    }
}
