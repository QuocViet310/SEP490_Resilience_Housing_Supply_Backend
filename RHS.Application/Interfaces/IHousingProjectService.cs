using RHS.Application.DTOs.HousingProjects;

namespace RHS.Application.Interfaces;

public interface IHousingProjectService
{
    Task<PagedResultDto<HousingProjectResponseDto>> GetHousingProjectsAsync(
        HousingProjectFilterRequestDto request);
}
