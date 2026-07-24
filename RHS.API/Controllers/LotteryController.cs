using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.Lottery;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using System.Security.Claims;

namespace RHS.API.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/lottery")]
[Authorize]
public class LotteryController : ControllerBase
{
    private readonly ILotteryService _lotteryService;

    public LotteryController(ILotteryService lotteryService)
    {
        _lotteryService = lotteryService;
    }

    /// <summary>[CĐT] Lên lịch bốc thăm (Live Session Setup).</summary>
    [HttpPost("schedule")]
    [Authorize(Roles = RoleConstants.HousingDeveloper)]
    public async Task<IActionResult> Schedule(
        Guid projectId,
        [FromBody] CreateOrUpdateLotteryScheduleDto request,
        CancellationToken ct)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _lotteryService.ScheduleLotteryAsync(projectId, request, userId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>[Sở Xây dựng/Admin] Phê duyệt & công bố lịch bốc thăm.</summary>
    [HttpPost("schedule/approve")]
    [Authorize(Roles = $"{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator}")]
    public async Task<IActionResult> ApproveSchedule(
        Guid projectId,
        CancellationToken ct)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _lotteryService.ApproveLotteryScheduleAsync(projectId, userId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>[Công khai] Xem lịch và thông tin phiên bốc thăm của dự án.</summary>
    [HttpGet("schedule")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSchedule(Guid projectId, CancellationToken ct)
    {
        var result = await _lotteryService.GetLotteryScheduleAsync(projectId, ct);
        if (result is null)
            return NotFound(new { message = "Không tìm thấy thông tin dự án." });
        return Ok(result);
    }

    /// <summary>[CĐT/Sở/Admin] Xem danh sách ứng viên đủ điều kiện bốc thăm (APPROVED / APPROVED_BY_TIMEOUT).</summary>
    [HttpGet("eligible-participants")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator}")]
    public async Task<IActionResult> GetEligibleParticipants(Guid projectId, CancellationToken ct)
    {
        var result = await _lotteryService.GetEligibleParticipantsAsync(projectId, ct);
        return Ok(result);
    }

    /// <summary>[Applicant] Bốc thăm tương tác thời gian thực với SemaphoreSlim Concurrency Lock (Mục 21).</summary>
    [HttpPost("draw-unit")]
    [Authorize(Roles = RoleConstants.Applicant)]
    public async Task<IActionResult> DrawUnitRealtime(Guid projectId, CancellationToken ct)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _lotteryService.DrawUnitRealtimeAsync(projectId, userId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>[CĐT] Chạy bốc thăm theo Đ38.2.</summary>
    [HttpPost("run")]
    [Authorize(Roles = RoleConstants.HousingDeveloper)]
    public async Task<IActionResult> Run(
        Guid projectId,
        [FromBody] RunLotteryRequestDto? request,
        CancellationToken ct)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _lotteryService.RunLotteryAsync(
                projectId, userId, request?.TotalUnits, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Xem kết quả bốc thăm mới nhất.</summary>
    [HttpGet("result")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator},{RoleConstants.Applicant}")]
    public async Task<IActionResult> GetResult(Guid projectId, CancellationToken ct)
    {
        var result = await _lotteryService.GetLatestResultAsync(projectId, ct);
        if (result is null)
            return NotFound(new { message = "Chưa có kết quả bốc thăm cho dự án này." });
        return Ok(result);
    }

    /// <summary>[CĐT] Mở sảnh chờ (WaitingLobby).</summary>
    [HttpPost("session/open-lobby")]
    [Authorize(Roles = RoleConstants.HousingDeveloper)]
    public async Task<IActionResult> OpenLobby(Guid projectId, CancellationToken ct)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            return Ok(await _lotteryService.OpenLobbyAsync(projectId, userId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>[CĐT] Bắt đầu bốc thăm live (Live).</summary>
    [HttpPost("session/start")]
    [Authorize(Roles = RoleConstants.HousingDeveloper)]
    public async Task<IActionResult> StartLive(Guid projectId, CancellationToken ct)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            return Ok(await _lotteryService.StartLiveAsync(projectId, userId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>[CĐT] Kết thúc phiên (Finished) — chốt người chưa bốc = trượt.</summary>
    [HttpPost("session/finish")]
    [Authorize(Roles = RoleConstants.HousingDeveloper)]
    public async Task<IActionResult> FinishSession(Guid projectId, CancellationToken ct)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            return Ok(await _lotteryService.FinishSessionAsync(projectId, userId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>[Sở/Admin] Công bố kết quả (Published) — CĐT không tự công bố (Đ36.2.b).</summary>
    [HttpPost("session/publish")]
    [Authorize(Roles = $"{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator}")]
    public async Task<IActionResult> PublishSession(Guid projectId, CancellationToken ct)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            return Ok(await _lotteryService.PublishSessionAsync(projectId, userId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Xác thực OTP vào sảnh (Applicant). Staff không cần mã.</summary>
    [HttpPost("session/verify-otp")]
    [Authorize]
    public async Task<IActionResult> VerifyOtp(
        Guid projectId,
        [FromBody] JoinLotteryLobbyRequestDto request,
        CancellationToken ct)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var isStaff = User.IsInRole(RoleConstants.HousingDeveloper)
                          || User.IsInRole(RoleConstants.DepartmentOfConstruction)
                          || User.IsInRole(RoleConstants.SystemAdministrator);
            var result = await _lotteryService.VerifyJoinCodeAsync(
                projectId, userId, request.JoinCode, isStaff, ct);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Tải biên bản PDF phiên bốc thăm.</summary>
    [HttpGet("minutes.pdf")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator}")]
    public async Task<IActionResult> DownloadMinutes(
        Guid projectId,
        [FromServices] IReportExportService reports)
    {
        var bytes = await reports.ExportLotteryMinutesPdfAsync(projectId);
        return File(bytes, "application/pdf", $"BienBan_BocTham_{projectId:N}.pdf");
    }
}
