using Microsoft.EntityFrameworkCore;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Repositories;

public class HousingProjectRepository : IHousingProjectRepository
{
    private readonly AppDbContext _context;

    public HousingProjectRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDto<HousingProjectResponseDto>> GetHousingProjectsAsync(
        HousingProjectFilterRequestDto request)
    {
        // Build the query with filtering
        IQueryable<HousingProject> query = _context.HousingProjects
            .Include(x => x.HousingProjectStatus)
            .Include(x => x.ProjectImages)
            .AsNoTracking();

        // Apply search filter (by project name)
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchTerm = request.Search.ToLower();
            query = query.Where(x => x.ProjectName.ToLower().Contains(searchTerm));
        }

        // Apply province filter
        if (!string.IsNullOrWhiteSpace(request.Province))
        {
            query = query.Where(x => x.Province == request.Province);
        }

        // Apply district filter
        if (!string.IsNullOrWhiteSpace(request.District))
        {
            query = query.Where(x => x.District == request.District);
        }

        // Apply min price filter
        if (request.MinPrice.HasValue)
        {
            query = query.Where(x => x.MaxPrice >= request.MinPrice.Value);
        }

        // Apply max price filter
        if (request.MaxPrice.HasValue)
        {
            query = query.Where(x => x.MinPrice <= request.MaxPrice.Value);
        }

        // Apply min area filter
        if (request.MinArea.HasValue)
        {
            query = query.Where(x => x.MaxArea >= request.MinArea.Value);
        }

        // Apply max area filter
        if (request.MaxArea.HasValue)
        {
            query = query.Where(x => x.MinArea <= request.MaxArea.Value);
        }

        // Apply status filter
        if (request.StatusId.HasValue)
        {
            query = query.Where(x => x.HousingProjectStatusId == request.StatusId.Value);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting (newest first)
        query = query.OrderByDescending(x => x.CreatedAt);

        // Apply pagination
        var pageIndex = Math.Max(request.PageIndex, 1);
        var pageSize = Math.Max(request.PageSize, 1);
        var skip = (pageIndex - 1) * pageSize;

        var projects = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        // Map to DTOs
        var items = projects.Select(x => new HousingProjectResponseDto
        {
            Id = x.Id,
            ProjectName = x.ProjectName,
            Description = x.Description,
            Province = x.Province,
            District = x.District,
            Address = x.Address,
            MinPrice = x.MinPrice,
            MaxPrice = x.MaxPrice,
            MinArea = x.MinArea,
            MaxArea = x.MaxArea,
            AvailableUnits = x.AvailableUnits,
            ThumbnailUrl = x.ThumbnailUrl,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
            Status = x.HousingProjectStatus?.StatusName,
            Images = x.ProjectImages
                .OrderBy(p => p.DisplayOrder)
                .Select(p => new ProjectImageResponseDto
                {
                    Id = p.Id,
                    ImageUrl = p.ImageUrl,
                    DisplayOrder = p.DisplayOrder
                })
                .ToList()
        }).ToList();

        return new PagedResultDto<HousingProjectResponseDto>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        };
    }

    public async Task<HousingProject> CreateAsync(HousingProject entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        _context.HousingProjects.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<HousingProject?> GetByIdAsync(Guid id)
    {
        return await _context.HousingProjects
            .Include(x => x.HousingProjectStatus)
            .Include(x => x.ProjectImages)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task UpdateAsync(HousingProject entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;

        var existingImages = await _context.ProjectImages
            .Where(x => x.ProjectId == entity.Id)
            .ToListAsync();
        _context.ProjectImages.RemoveRange(existingImages);

        _context.HousingProjects.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(HousingProject entity)
    {
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        _context.HousingProjects.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.HousingProjects
            .AsNoTracking()
            .AnyAsync(x => x.Id == id);
    }

    public async Task<bool> StatusExistsAsync(Guid statusId)
    {
        return await _context.HousingProjectStatuses
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(x => x.Id == statusId);
    }
}
