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
    /// Kiểm tra xem một số CCCD đã được đăng ký bởi user nào đó trong hệ thống chưa.
    /// </summary>
    /// <param name="citizenId">Số CCCD cần kiểm tra.</param>
    /// <param name="excludeUserId">
    /// Nếu cung cấp, sẽ bỏ qua user này khi tìm kiếm.
    /// Dùng để cho phép user xác thực lại CCCD của chính mình.
    /// </param>
    /// <returns><c>true</c> nếu CCCD đã thuộc user khác; <c>false</c> nếu chưa có ai dùng.</returns>
    Task<bool> CitizenIdExistsAsync(string citizenId, Guid? excludeUserId = null);

    // New methods for staff management
    Task<List<User>> GetByRoleAsync(string roleName);
    Task<List<User>> GetStaffListAsync(int pageNumber, int pageSize, string? role = null, string? status = null, string? searchTerm = null);
    Task<int> GetStaffCountAsync(string? role = null, string? status = null, string? searchTerm = null);
}
