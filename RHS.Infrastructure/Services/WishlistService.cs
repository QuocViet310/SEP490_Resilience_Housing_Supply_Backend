using RHS.Application.DTOs.HousingProjects;
using RHS.Application.DTOs.Wishlist;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Implement business logic cho tính năng Wishlist.
/// Validate project tồn tại, kiểm tra duplicate trước khi delegate xuống repository.
/// </summary>
public class WishlistService : IWishlistService
{
    private readonly IWishlistRepository      _wishlistRepository;
    private readonly IHousingProjectRepository _projectRepository;

    public WishlistService(
        IWishlistRepository      wishlistRepository,
        IHousingProjectRepository projectRepository)
    {
        _wishlistRepository = wishlistRepository;
        _projectRepository  = projectRepository;
    }

    /// <inheritdoc/>
    public async Task AddToWishlistAsync(Guid userId, Guid projectId)
    {
        // Validate: project phải tồn tại
        var projectExists = await _projectRepository.ExistsAsync(projectId);
        if (!projectExists)
            throw new KeyNotFoundException($"Không tìm thấy dự án với Id '{projectId}'.");

        // Validate: không thêm trùng
        var alreadyExists = await _wishlistRepository.ExistsAsync(userId, projectId);
        if (alreadyExists)
            throw new InvalidOperationException("Dự án này đã có trong danh sách yêu thích của bạn.");

        var wishlist = new Wishlist
        {
            Id               = Guid.NewGuid(),
            UserId           = userId,
            HousingProjectId = projectId,
            CreatedAt        = DateTime.UtcNow
        };

        await _wishlistRepository.AddAsync(wishlist);
    }

    /// <inheritdoc/>
    public async Task RemoveFromWishlistAsync(Guid userId, Guid projectId)
    {
        // Validate: item phải đang có trong wishlist
        var exists = await _wishlistRepository.ExistsAsync(userId, projectId);
        if (!exists)
            throw new KeyNotFoundException("Dự án này không có trong danh sách yêu thích của bạn.");

        await _wishlistRepository.RemoveAsync(userId, projectId);
    }

    /// <inheritdoc/>
    public async Task<PagedResultDto<WishlistItemResponseDto>> GetWishlistAsync(
        Guid userId,
        int  pageIndex = 1,
        int  pageSize  = 10)
        => await _wishlistRepository.GetByUserIdAsync(userId, pageIndex, pageSize);

    /// <inheritdoc/>
    public async Task<bool> IsInWishlistAsync(Guid userId, Guid projectId)
        => await _wishlistRepository.ExistsAsync(userId, projectId);

    /// <inheritdoc/>
    public async Task<WishlistItemResponseDto?> GetWishlistItemByProjectAsync(Guid userId, Guid projectId)
        => await _wishlistRepository.GetByUserAndProjectAsync(userId, projectId);
}
