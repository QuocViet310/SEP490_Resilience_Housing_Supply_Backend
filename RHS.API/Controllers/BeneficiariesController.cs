using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.Beneficiaries;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;

namespace RHS.API.Controllers;

/// <summary>Đ44 — Công bố danh sách đối tượng đã được phân suất (WON / PRIORITY_WON).</summary>
[ApiController]
[Route("api/beneficiaries")]
[Authorize]
public class BeneficiariesController : ControllerBase
{
    private readonly IBeneficiaryPublishService _beneficiaryService;

    public BeneficiariesController(IBeneficiaryPublishService beneficiaryService)
    {
        _beneficiaryService = beneficiaryService;
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleConstants.DepartmentOfConstruction},{RoleConstants.HousingDeveloper},{RoleConstants.SystemAdministrator}")]
    [ProducesResponseType(typeof(IReadOnlyList<BeneficiaryListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublished(
        [FromQuery] Guid? projectId,
        CancellationToken ct)
    {
        var list = await _beneficiaryService.GetPublishedBeneficiariesAsync(projectId, ct);
        return Ok(list);
    }
}
