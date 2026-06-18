using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.Interfaces;

namespace RHS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HousingProjectsController : ControllerBase
{
    private readonly IHousingProjectService _service;
    private readonly ILogger<HousingProjectsController> _logger;

    public HousingProjectsController(
        IHousingProjectService service,
        ILogger<HousingProjectsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Get housing projects with search and filtering support
    /// </summary>
    /// <param name="pageIndex">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (default: 12, max: 100)</param>
    /// <param name="search">Search by project name</param>
    /// <param name="province">Filter by province</param>
    /// <param name="district">Filter by district</param>
    /// <param name="minPrice">Minimum price</param>
    /// <param name="maxPrice">Maximum price</param>
    /// <param name="minArea">Minimum area</param>
    /// <param name="maxArea">Maximum area</param>
    /// <param name="statusId">Filter by status ID</param>
    /// <returns>Paginated list of housing projects</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResultDto<HousingProjectResponseDto>>> GetHousingProjects(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? search = null,
        [FromQuery] string? province = null,
        [FromQuery] string? district = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] double? minArea = null,
        [FromQuery] double? maxArea = null,
        [FromQuery] Guid? statusId = null)
    {
        try
        {
            var request = new HousingProjectFilterRequestDto
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                Search = search,
                Province = province,
                District = district,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                MinArea = minArea,
                MaxArea = maxArea,
                StatusId = statusId
            };

            var result = await _service.GetHousingProjectsAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrieving housing projects");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get housing project detail by ID
    /// </summary>
    /// <param name="id">Housing project ID</param>
    /// <returns>Housing project detail</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<HousingProjectResponseDto>> GetHousingProjectById(Guid id)
    {
        try
        {
            var result = await _service.GetHousingProjectByIdAsync(id);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Housing project not found with ID: {ProjectId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrieving housing project detail");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Create a new housing project (Admin/Officer only)
    /// </summary>
    /// <param name="request">Create housing project request</param>
    /// <returns>Created housing project</returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<HousingProjectResponseDto>> CreateHousingProject(
        [FromForm] CreateHousingProjectRequestDto request)
    {
        try
        {
            var result = await _service.CreateHousingProjectAsync(request);
            return CreatedAtAction(nameof(GetHousingProjectById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error while creating housing project");
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while creating housing project");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while creating housing project");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Update a housing project (Admin/Officer only)
    /// </summary>
    /// <param name="id">Housing project ID</param>
    /// <param name="request">Update housing project request</param>
    /// <returns>Updated housing project</returns>
    [HttpPut("{id}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<HousingProjectResponseDto>> UpdateHousingProject(
        Guid id,
        [FromForm] UpdateHousingProjectRequestDto request)
    {
        try
        {
            var result = await _service.UpdateHousingProjectAsync(id, request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error while updating housing project");
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while updating housing project");
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating housing project");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Delete a housing project - soft delete (Admin/Officer only)
    /// </summary>
    /// <param name="id">Housing project ID</param>
    /// <returns>No content</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteHousingProject(Guid id)
    {
        try
        {
            await _service.DeleteHousingProjectAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Housing project not found with ID: {ProjectId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting housing project");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while processing your request" });
        }
    }
}
