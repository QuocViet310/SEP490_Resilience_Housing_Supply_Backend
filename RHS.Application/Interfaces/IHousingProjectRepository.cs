using RHS.Application.DTOs.HousingProjects;
using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

public interface IHousingProjectRepository
{
    Task<PagedResultDto<HousingProjectResponseDto>> GetHousingProjectsAsync(
        HousingProjectFilterRequestDto request,
        string? residentWard = null);

    Task<HousingProject> CreateAsync(HousingProject entity);

    Task<HousingProject?> GetByIdAsync(Guid id);

    Task UpdateAsync(HousingProject entity);

    Task SoftDeleteAsync(HousingProject entity);

    Task<bool> ExistsAsync(Guid id);

    Task<bool> StatusExistsAsync(Guid statusId);
}
