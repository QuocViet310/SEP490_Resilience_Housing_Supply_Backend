using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.Policy;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using System.Security.Claims;

namespace RHS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleConstants.SystemAdministrator)]
public class PolicyConfigController : ControllerBase
{
    private readonly IPolicyService _policyService;

    public PolicyConfigController(IPolicyService policyService)
    {
        _policyService = policyService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PolicyConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var list = await _policyService.GetAllAsync(ct);
        return Ok(list);
    }

    [HttpGet("{policyName}")]
    [ProducesResponseType(typeof(PolicyConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByName(string policyName, CancellationToken ct)
    {
        var item = await _policyService.GetByNameAsync(policyName, ct);
        if (item is null)
            return NotFound(new { message = $"Không tìm thấy policy '{policyName}'." });
        return Ok(item);
    }

    [HttpPut("{policyName}")]
    [ProducesResponseType(typeof(PolicyConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        string policyName,
        [FromBody] UpdatePolicyValueRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PolicyValue))
            return BadRequest(new { message = "PolicyValue là bắt buộc." });

        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _policyService.UpdateValueAsync(policyName, request.PolicyValue, userId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
