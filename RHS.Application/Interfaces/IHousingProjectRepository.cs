using RHS.Application.DTOs.HousingProjects;

namespace RHS.Application.Interfaces;

public interface IHousingProjectRepository
{
    Task<PagedResultDto<HousingProjectResponseDto>> GetHousingProjectsAsync(
        HousingProjectFilterRequestDto request);
}
