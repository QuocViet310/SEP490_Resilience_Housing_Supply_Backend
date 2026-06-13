using RHS.Application.DTOs.HousingProjects;

namespace RHS.Application.Interfaces;

public interface IHousingProjectStatusService
{
    Task<List<HousingProjectStatusResponseDto>> GetAllStatusesAsync();
}
