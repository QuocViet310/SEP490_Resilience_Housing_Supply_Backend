using RHS.Application.DTOs.HousingProjects;
using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

public interface IHousingProjectRepository
{
    Task<PagedResultDto<HousingProjectResponseDto>> GetHousingProjectsAsync(
        HousingProjectFilterRequestDto request,
        Guid? currentUserId = null,
        string? currentUserRole = null);

    Task<HousingProject> CreateAsync(HousingProject entity);

    Task<HousingProject?> GetByIdAsync(Guid id);

    Task UpdateAsync(HousingProject entity);

    /// <summary>
    /// Chỉ ghi các cột trạng thái / công bố (PENDING→UPCOMING, lifecycle).
    /// Không đụng ProjectImages, Apartments, PaymentMilestones.
    /// </summary>
    Task UpdateStatusOnlyAsync(HousingProject entity);

    Task SoftDeleteAsync(HousingProject entity);

    Task<bool> ExistsAsync(Guid id);

    Task<bool> StatusExistsAsync(Guid statusId);

    Task<HousingProjectStatus?> GetStatusByCodeAsync(string code);

    Task<HousingProject?> GetActiveProjectByNameAsync(string projectName, Guid? developerId = null, Guid? excludeProjectId = null);
}
