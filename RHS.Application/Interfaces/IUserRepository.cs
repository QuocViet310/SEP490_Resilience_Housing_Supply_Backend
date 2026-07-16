using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByGoogleIdAsync(string googleId);
    Task<User> CreateAsync(User user);
    Task UpdateAsync(User user);
    Task<bool> EmailExistsAsync(string email);

    /// <summary>
    /// Kiểm tra CCCD đã gắn với tài khoản <c>Active</c> khác chưa.
    /// Tài khoản soft-delete (Status=Deleted) không được tính.
    /// </summary>
    /// <param name="citizenId">Số CCCD cần kiểm tra.</param>
    /// <param name="excludeUserId">
    /// Nếu cung cấp, bỏ qua user này (cho phép xác thực lại CCCD của chính mình).
    /// </param>
    Task<bool> CitizenIdExistsAsync(string citizenId, Guid? excludeUserId = null);

    // New methods for staff management
    Task<List<User>> GetByRoleAsync(string roleName);
    Task<List<User>> GetStaffListAsync(int pageNumber, int pageSize, string? role = null, string? status = null, string? searchTerm = null);
    Task<int> GetStaffCountAsync(string? role = null, string? status = null, string? searchTerm = null);
}
