using Microsoft.EntityFrameworkCore;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.Interfaces;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Services;

public class HousingProjectStatusService : IHousingProjectStatusService
{
    private readonly AppDbContext _context;

    public HousingProjectStatusService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<HousingProjectStatusResponseDto>> GetAllStatusesAsync()
    {
        var statuses = await _context.HousingProjectStatuses
            .AsNoTracking()
            .OrderBy(x => x.StatusName)
            .Select(x => new HousingProjectStatusResponseDto
            {
                Id = x.Id,
                StatusName = x.StatusName,
                StatusCode = x.StatusCode
            })
            .ToListAsync();

        return statuses;
    }
}
