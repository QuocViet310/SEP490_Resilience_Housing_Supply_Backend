using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.Interfaces;

namespace RHS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HousingProjectStatusesController : ControllerBase
{
    private readonly IHousingProjectStatusService _service;
    private readonly ILogger<HousingProjectStatusesController> _logger;

    public HousingProjectStatusesController(
        IHousingProjectStatusService service,
        ILogger<HousingProjectStatusesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Get all available housing project statuses
    /// </summary>
    /// <returns>List of housing project statuses</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<HousingProjectStatusResponseDto>>> GetAllStatuses()
    {
        try
        {
            var result = await _service.GetAllStatusesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrieving housing project statuses");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while processing your request" });
        }
    }
}
