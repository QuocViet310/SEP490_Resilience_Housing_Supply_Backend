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
}
