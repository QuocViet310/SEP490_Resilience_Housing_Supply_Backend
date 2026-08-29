using System.Text.Json;
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

    /// <summary>Lấy toàn bộ danh sách cấu hình chính sách hệ thống</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PolicyConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var list = await _policyService.GetAllAsync(ct);
        return Ok(list);
    }

    /// <summary>Lấy chi tiết 1 chính sách theo PolicyName</summary>
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

    /// <summary>Cập nhật giá trị chính sách hệ thống (diện tích, trần thu nhập, tỷ lệ lãi phạt...)</summary>
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

    /// <summary>Lấy cấu hình bảng thang điểm ưu tiên cho các đối tượng thụ hưởng</summary>
    [HttpGet("priority-points")]
    [ProducesResponseType(typeof(PriorityPointsTableDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPriorityPointsTable(CancellationToken ct)
    {
        var item = await _policyService.GetByNameAsync(PolicyKeys.PriorityPointsTableJson, ct);
        if (item is null || string.IsNullOrWhiteSpace(item.PolicyValue))
        {
            return Ok(new PriorityPointsTableDto());
        }

        try
        {
            var table = JsonSerializer.Deserialize<List<PriorityGroupPointItemDto>>(item.PolicyValue) ?? new List<PriorityGroupPointItemDto>();
            return Ok(new PriorityPointsTableDto { PointsTable = table });
        }
        catch
        {
            return Ok(new PriorityPointsTableDto());
        }
    }

    /// <summary>Cập nhật bảng thang điểm ưu tiên cho các đối tượng thụ hưởng</summary>
    [HttpPut("priority-points")]
    [ProducesResponseType(typeof(PolicyConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePriorityPointsTable([FromBody] PriorityPointsTableDto dto, CancellationToken ct)
    {
        if (dto.PointsTable is null || dto.PointsTable.Count == 0)
            return BadRequest(new { message = "PointsTable không được để trống." });

        string json = JsonSerializer.Serialize(dto.PointsTable);
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
        var result = await _policyService.UpdateValueAsync(PolicyKeys.PriorityPointsTableJson, json, userId, ct);
        return Ok(result);
    }
}
