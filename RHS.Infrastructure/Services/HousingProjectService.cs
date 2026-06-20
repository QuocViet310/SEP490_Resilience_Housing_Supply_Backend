using RHS.Application.DTOs.HousingProjects;
using RHS.Application.Interfaces;

namespace RHS.Infrastructure.Services;

public class HousingProjectService : IHousingProjectService
{
    private readonly IHousingProjectRepository _repository;

    public HousingProjectService(IHousingProjectRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDto<HousingProjectResponseDto>> GetHousingProjectsAsync(
        HousingProjectFilterRequestDto request)
    {
        // Validate request
        if (request.PageIndex < 1)
            request.PageIndex = 1;

        if (request.PageSize < 1)
            request.PageSize = 12;

        if (request.PageSize > 100)
            request.PageSize = 100;

        // Call repository to get paginated results
        return await _repository.GetHousingProjectsAsync(request);
    }
}
