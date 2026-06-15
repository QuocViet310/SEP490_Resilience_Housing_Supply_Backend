using RHS.Application.DTOs.HousingProjects;
using RHS.Application.DTOs.Wishlist;
using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

/// <summary>
/// Repository interface cho Wishlist.
/// Chỉ chứa các thao tác data access thuần — business logic nằm ở IWishlistService.
/// </summary>
public interface IWishlistRepository
{
    /// <summary>
    /// Kiểm tra xem một HousingProject đã có trong wishlist của user chưa.
    /// </summary>
    Task<bool> ExistsAsync(Guid userId, Guid projectId);

    /// <summary>
    /// Thêm một mục mới vào wishlist.
    /// </summary>
    Task AddAsync(Wishlist wishlist);

    /// <summary>
    /// Xóa một mục khỏi wishlist theo userId + projectId.
    /// Không ném exception nếu không tìm thấy (no-op).
    /// </summary>
    Task RemoveAsync(Guid userId, Guid projectId);

    /// <summary>
    /// Lấy toàn bộ wishlist của một user, phân trang, sắp xếp mới nhất trước.
    /// </summary>
    Task<PagedResultDto<WishlistItemResponseDto>> GetByUserIdAsync(
        Guid userId,
        int pageIndex,
        int pageSize);

    /// <summary>
    /// Lấy một wishlist item cụ thể theo userId + projectId.
    /// Trả về null nếu chưa có trong wishlist.
    /// </summary>
    Task<WishlistItemResponseDto?> GetByUserAndProjectAsync(Guid userId, Guid projectId);
}
