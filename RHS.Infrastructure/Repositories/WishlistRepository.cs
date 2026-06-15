using Microsoft.EntityFrameworkCore;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.DTOs.Wishlist;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Repositories;

public class WishlistRepository : IWishlistRepository
{
    private readonly AppDbContext _context;

    public WishlistRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(Guid userId, Guid projectId)
        => await _context.Wishlists
            .AsNoTracking()
            .AnyAsync(w => w.UserId == userId && w.HousingProjectId == projectId);

    /// <inheritdoc/>
    public async Task AddAsync(Wishlist wishlist)
    {
        _context.Wishlists.Add(wishlist);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(Guid userId, Guid projectId)
    {
        var wishlist = await _context.Wishlists
            .FirstOrDefaultAsync(w => w.UserId == userId && w.HousingProjectId == projectId);

        if (wishlist is null) return; // no-op nếu không tìm thấy

        _context.Wishlists.Remove(wishlist);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task<PagedResultDto<WishlistItemResponseDto>> GetByUserIdAsync(
        Guid userId,
        int pageIndex,
        int pageSize)
    {
        var query = _context.Wishlists
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .Include(w => w.HousingProject)
                .ThenInclude(p => p.HousingProjectStatus);

        var totalCount = await query.CountAsync();

        var pageIndex1 = Math.Max(pageIndex, 1);
        var pageSize1  = Math.Max(pageSize, 1);

        var items = await query
            .OrderByDescending(w => w.CreatedAt)
            .Skip((pageIndex1 - 1) * pageSize1)
            .Take(pageSize1)
            .Select(w => new WishlistItemResponseDto
            {
                WishlistId     = w.Id,
                AddedAt        = w.CreatedAt,
                ProjectId      = w.HousingProject.Id,
                ProjectName    = w.HousingProject.ProjectName,
                Description    = w.HousingProject.Description,
                Province       = w.HousingProject.Province,
                District       = w.HousingProject.District,
                Address        = w.HousingProject.Address,
                MinPrice       = w.HousingProject.MinPrice,
                MaxPrice       = w.HousingProject.MaxPrice,
                MinArea        = w.HousingProject.MinArea,
                MaxArea        = w.HousingProject.MaxArea,
                AvailableUnits = w.HousingProject.AvailableUnits,
                ThumbnailUrl   = w.HousingProject.ThumbnailUrl,
                Status         = w.HousingProject.HousingProjectStatus != null
                                 ? w.HousingProject.HousingProjectStatus.StatusName
                                 : null
            })
            .ToListAsync();

        return new PagedResultDto<WishlistItemResponseDto>
        {
            PageIndex  = pageIndex1,
            PageSize   = pageSize1,
            TotalCount = totalCount,
            Items      = items
        };
    }

    /// <inheritdoc/>
    public async Task<WishlistItemResponseDto?> GetByUserAndProjectAsync(Guid userId, Guid projectId)
        => await _context.Wishlists
            .AsNoTracking()
            .Where(w => w.UserId == userId && w.HousingProjectId == projectId)
            .Include(w => w.HousingProject)
                .ThenInclude(p => p.HousingProjectStatus)
            .Select(w => new WishlistItemResponseDto
            {
                WishlistId     = w.Id,
                AddedAt        = w.CreatedAt,
                ProjectId      = w.HousingProject.Id,
                ProjectName    = w.HousingProject.ProjectName,
                Description    = w.HousingProject.Description,
                Province       = w.HousingProject.Province,
                District       = w.HousingProject.District,
                Address        = w.HousingProject.Address,
                MinPrice       = w.HousingProject.MinPrice,
                MaxPrice       = w.HousingProject.MaxPrice,
                MinArea        = w.HousingProject.MinArea,
                MaxArea        = w.HousingProject.MaxArea,
                AvailableUnits = w.HousingProject.AvailableUnits,
                ThumbnailUrl   = w.HousingProject.ThumbnailUrl,
                Status         = w.HousingProject.HousingProjectStatus != null
                                 ? w.HousingProject.HousingProjectStatus.StatusName
                                 : null
            })
            .FirstOrDefaultAsync();
}
