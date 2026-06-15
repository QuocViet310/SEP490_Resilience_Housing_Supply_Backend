using RHS.Application.DTOs.HousingProjects;
using RHS.Application.DTOs.Wishlist;

namespace RHS.Application.Interfaces;

/// <summary>
/// Service interface cho tính năng Wishlist.
/// Chứa business logic: validate project tồn tại, kiểm tra duplicate, v.v.
/// </summary>
public interface IWishlistService
{
    /// <summary>
    /// Thêm một HousingProject vào wishlist của user.
    /// Ném <see cref="KeyNotFoundException"/> nếu project không tồn tại.
    /// Ném <see cref="InvalidOperationException"/> nếu project đã có trong wishlist.
    /// </summary>
    Task AddToWishlistAsync(Guid userId, Guid projectId);

    /// <summary>
    /// Xóa một HousingProject khỏi wishlist của user.
    /// Ném <see cref="KeyNotFoundException"/> nếu item không có trong wishlist.
    /// </summary>
    Task RemoveFromWishlistAsync(Guid userId, Guid projectId);

    /// <summary>
    /// Lấy danh sách wishlist của user có phân trang, mới nhất trước.
    /// </summary>
    Task<PagedResultDto<WishlistItemResponseDto>> GetWishlistAsync(
        Guid userId,
        int pageIndex = 1,
        int pageSize  = 10);

    /// <summary>
    /// Kiểm tra nhanh xem một project có đang trong wishlist của user không.
    /// </summary>
    Task<bool> IsInWishlistAsync(Guid userId, Guid projectId);

    /// <summary>
    /// Lấy thông tin một wishlist item theo projectId.
    /// Trả về null nếu user chưa thêm project đó vào wishlist.
    /// </summary>
    Task<WishlistItemResponseDto?> GetWishlistItemByProjectAsync(Guid userId, Guid projectId);
}
