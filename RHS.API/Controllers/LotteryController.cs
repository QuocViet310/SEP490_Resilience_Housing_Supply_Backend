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
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.DepartmentOfConstruction},{RoleConstants.Applicant}")]
    public async Task<IActionResult> GetResult(Guid projectId, CancellationToken ct)
    {
        var result = await _lotteryService.GetLatestResultAsync(projectId, ct);
        if (result is null)
            return NotFound(new { message = "Chưa có kết quả bốc thăm cho dự án này." });
        return Ok(result);
    }
}
