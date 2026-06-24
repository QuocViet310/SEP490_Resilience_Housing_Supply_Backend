using RHS.Application.DTOs.HousingProjects;

namespace RHS.Application.Interfaces;

public interface IHousingProjectService
{
    Task<PagedResultDto<HousingProjectResponseDto>> GetHousingProjectsAsync(
        HousingProjectFilterRequestDto request,
        string? residentWard = null);

    Task<HousingProjectResponseDto> CreateHousingProjectAsync(
        CreateHousingProjectRequestDto request);

    Task<HousingProjectResponseDto> UpdateHousingProjectAsync(
        Guid id,
        UpdateHousingProjectRequestDto request);

    Task DeleteHousingProjectAsync(Guid id);

    Task<HousingProjectResponseDto> GetHousingProjectByIdAsync(Guid id);
}
