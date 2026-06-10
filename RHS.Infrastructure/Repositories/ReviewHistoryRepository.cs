using Microsoft.EntityFrameworkCore;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Repositories;

public class ReviewHistoryRepository : IReviewHistoryRepository
{
    private readonly AppDbContext _context;

    public ReviewHistoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApplicationStatusHistory> CreateAsync(ApplicationStatusHistory history)
    {
        _context.ApplicationStatusHistories.Add(history);
        await _context.SaveChangesAsync();
        return history;
    }

    public async Task<IReadOnlyList<ApplicationStatusHistory>> GetByApplicationIdAsync(
        Guid applicationId)
    {
        return await _context.ApplicationStatusHistories
            .AsNoTracking()
            .Include(h => h.ChangedByUser)
            .Where(h => h.ApplicationId == applicationId)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();
    }
}
