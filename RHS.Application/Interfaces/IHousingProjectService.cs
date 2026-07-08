using RHS.Application.DTOs.HousingProjects;

namespace RHS.Application.Interfaces;

public interface IHousingProjectService
{
    Task<PagedResultDto<HousingProjectResponseDto>> GetHousingProjectsAsync(
        HousingProjectFilterRequestDto request,
        Guid? currentUserId = null,
        string? currentUserRole = null);

    Task<HousingProjectResponseDto> CreateHousingProjectAsync(
        CreateHousingProjectRequestDto request,
        Guid? developerId = null);

    Task<HousingProjectResponseDto> UpdateHousingProjectAsync(
        Guid id,
        UpdateHousingProjectRequestDto request);

    Task DeleteHousingProjectAsync(Guid id);

    Task<HousingProjectResponseDto> GetHousingProjectByIdAsync(Guid id);

    Task<HousingProjectResponseDto> UpdateProjectStatusAsync(
        Guid id,
        string action,
        string? rejectReason);
}
