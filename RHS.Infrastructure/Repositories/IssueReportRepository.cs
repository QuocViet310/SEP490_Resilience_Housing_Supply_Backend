using Microsoft.EntityFrameworkCore;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Repositories;

public class IssueReportRepository : IIssueReportRepository
{
    private readonly AppDbContext _context;

    public IssueReportRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IssueReport> CreateAsync(IssueReport entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        _context.IssueReports.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<IssueReport?> GetByIdAsync(Guid id)
    {
        return await _context.IssueReports
            .Include(x => x.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<PagedResultDto<IssueReport>> GetPagedAsync(
        int pageIndex,
        int pageSize,
        string? search = null,
        string? status = null,
        string? issueType = null)
    {
        IQueryable<IssueReport> query = _context.IssueReports
            .Include(x => x.User)
            .AsNoTracking();

        // Apply search filter (by title)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();
            query = query.Where(x => x.Title.ToLower().Contains(searchTerm));
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        // Apply issue type filter
        if (!string.IsNullOrWhiteSpace(issueType))
        {
            query = query.Where(x => x.IssueType == issueType);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting (newest first)
        query = query.OrderByDescending(x => x.CreatedAt);

        // Apply pagination
        pageIndex = Math.Max(pageIndex, 1);
        pageSize = Math.Max(pageSize, 1);
        pageSize = Math.Min(pageSize, 100);
        var skip = (pageIndex - 1) * pageSize;

        var issues = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<IssueReport>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = issues
        };
    }

    public async Task<PagedResultDto<IssueReport>> GetByUserIdAsync(
        Guid userId,
        int pageIndex,
        int pageSize)
    {
        IQueryable<IssueReport> query = _context.IssueReports
            .Where(x => x.UserId == userId)
            .Include(x => x.User)
            .AsNoTracking();

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting (newest first)
        query = query.OrderByDescending(x => x.CreatedAt);

        // Apply pagination
        pageIndex = Math.Max(pageIndex, 1);
        pageSize = Math.Max(pageSize, 1);
        pageSize = Math.Min(pageSize, 100);
        var skip = (pageIndex - 1) * pageSize;

        var issues = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<IssueReport>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = issues
        };
    }

    public async Task UpdateAsync(IssueReport entity)
    {
        _context.IssueReports.Update(entity);
        await _context.SaveChangesAsync();
    }
}
